# 当前测试用例总表

更新：2026-09-06。本轮每手结算、排行榜、观战摊牌及 UI 调整后，Unity 2022.3.52f1c1 全量 EditMode **337/337 通过**，作业 `a630f1efb5fe44b3a1e11f455be96928`。包含新增 `HandSettlementAndLeaderboardTest` 的 33 个展开用例，覆盖完整净额清单、原子回滚、旧 schema 恢复和升级失败回滚、重放、排行榜身份／排序／协议、任务边界及金额格式。修改了旧测试中逐次扣款、严格小于 1,000、观战结算不显示公开牌的过时预期。

最新联调及构建记录见 [本轮验证记录](HandSettlementValidation.md)，最终布局修改后 72 项 UI 测试再次通过。下列统计和编号清单来自此前版本，保留作历史索引，不能代替本轮 337 项的运行结果。

## 此前版本记录

- 自有 NUnit EditMode：**269 项、206 个方法、15 个测试类、13 个文件**。参数化用例按参数展开，循环断言不重复计数。
- 上一版本在 Unity 2022.3.52f1c1 的完整结果：**257/257 通过**，作业 `46a221852c734f2aa15b10f24f533aef`。
- 上一版本最后视觉修正后 UIInteractionTest 单独复跑 **40/40 通过**，作业 `dbadc520ae64486d905993bb31e9793a`。
- 每日任务功能完成时曾执行全部自有 EditMode 测试；新增 39 项，见 [每日任务说明](D:/GitHubRepos/Poker/Docs/DailyTasks.md)。
- 上轮新增网络配置用例 25 项，见 [部署配置说明](D:/GitHubRepos/Poker/Docs/NetworkConfiguration.md)。
- 上轮新增导航用例 18 项，见 [导航说明](D:/GitHubRepos/Poker/Docs/UINavigationReview.md)。
- 上轮新增提示交互用例 18 项；此前新增停服用例 12 项，覆盖存储异常、停止所有牌局、重启退款与提示 UI；见 [停服规则](D:/GitHubRepos/Poker/Docs/FatalStorageShutdown.md)。
- 上一轮相比 119 项，增加 21 项旁观/座位/过期请求测试与 5 项真实 UI prefab 测试；另更新了与旧自动入座规则冲突的样例。
- 独立进程场景包括默认、departures、spectators、storage-allin、storage-normal，以及本轮新增的 tasks。最新一服三端任务、预约离开、停服及恢复记录见 [每日任务验证](D:/GitHubRepos/Poker/Docs/DailyTasks.md)；这些检查点不额外计入 NUnit 数量。
- 7 个旧手动脚本、20 项旧客户端验收步骤及临时 review 探针均保留说明，但不计入 269，也不因为文档列出便视为已执行。

## 当前 UI 流程

大厅点击“牌局中”的房间后先进入旁观，**不会自动占座或自动参加下一手**，满桌也可旁观。牌桌提供“下一手参战”“继续旁观”“离开房间”；有名额且筹码至少够一个大盲时可申请，申请期间仍无座号、无底牌，点击继续旁观可取消。下一手开局前才正式入座发牌。

本手玩家可点击“本手后旁观”，当前手照常行动并结算，保留 10 秒查看结果时间，之后才执行预约并释放座位成为旁观者；仍保留独立的“本手后离开”路径。查看期间可立即离开或转旁观，不打断其他人的查看时间。查看结束后，若下一手合格人数不足两人，剩余参赛者回房间等待页，所有旁观者清除房间关联并返回大厅。具体条件与实际验证见上述生命周期文档。

标题页增加“每日任务”。未登录时先登录，成功后回到任务页；仍有关联房间时优先恢复房间。打开面板和领取结束均请求服务器状态，关闭后过期响应不会重新打开页面。

## 自动化用例逐项清单

| 测试类 | 展开用例数 |
| --- | ---: |
| AccountServiceTest | 11 |
| PlayerManagerSessionTest | 1 |
| GameManagerRulesTest | 25 |
| RoomManagerSeatTest | 4 |
| HoldemRulesTest | 7 |
| LogTest | 1 |
| PokerTest | 35 |
| PotCalculatorTest | 8 |
| RoomDepartureTest | 11 |
| TableParticipationTest | 21 |
| UIInteractionTest | 40 |
| UINavigationTest | 28 |
| FatalStorageTest | 11 |
| NetworkConfigurationTest | 25 |
| DailyTaskTest | 29 |

### AccountServiceTest（11 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-001 | [PasswordHasherUsesUniqueSaltsAndRejectsWrongPassword](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:34) | 同一密码生成不同盐和哈希；正确密码通过，错误密码被拒绝。 |
| UT-002 | [PasswordLoginMigratesStoredWorkFactor](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:48) | 密码登录后，将账户保存的哈希迭代次数迁移到当前配置。 |
| UT-003 | [RegistrationPersistsInitialBalanceAndLedger](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:67) | 注册获得 10,000 筹码；重新打开数据库后保留账户及余额，初始发放账本只有一笔。 |
| UT-004 | [SessionTokenRotatesAndLogoutRevokesReplacement](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:97) | 令牌登录产生新令牌、旧令牌失效；登出后新令牌也失效。 |
| UT-005 | [SessionTokenExpiresAfterConfiguredLifetime](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:121) | 配置 30 天有效期，推进到第 31 天后拒绝令牌登录。 |
| UT-006 | [UserNamesAreCaseSensitiveAndExactDuplicatesAreRejected](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:140) | Alice 与 alice 可分别注册；完全同名注册、错误大小写登录被拒绝。 |
| UT-007 | [ChipMutationIsAtomicAndIdempotent](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:159) | 扣款成功后重放不重复扣款；余额不足的扣款不改变余额。 |
| UT-008 | [LastRoomPersistsAcrossRepositoryInstances](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:200) | 保存 LastRoomId 后，由新的仓储实例读取同一房间关联。 |
| UT-009 | [HandTransactionsAreAtomicConservativeAndIdempotent](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:215) | 一手内扣款与结算幂等、总筹码守恒；拒绝账户同时参加另一手及手内通用筹码变更。 |
| UT-010 | [InitializeAbortsAnUnsettledHandWithoutChangingBalances](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:283) | 新模式重新初始化只作废未结算手牌，余额不变，不产生退款流水。旧 schema 退款另由新增迁移用例覆盖。 |
| UT-011 | [ReconciliationTracksDeferredSettlementAndRecovery](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:324) | 核对本测试实际执行的下注中与结算后余额、投入、账本和活动手牌；崩溃恢复另见上一用例。 |

### PlayerManagerSessionTest（1 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-012 | [NewLoginReplacesOldConnectionWithoutAllowingStaleDisconnectToRemoveIt](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/AccountServiceTest.cs:410) | 新连接替代旧会话；旧连接的断线清理不能删除新连接。 |

### GameManagerRulesTest（25 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-013 | [CreateGame_RejectsNonFiniteThinkingTime(float.NaN)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:39) | 创建牌局时拒绝非有限思考时间并抛出 ArgumentException。 参数：float.NaN。 |
| UT-014 | [CreateGame_RejectsNonFiniteThinkingTime(float.PositiveInfinity)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:39) | 创建牌局时拒绝非有限思考时间并抛出 ArgumentException。 参数：float.PositiveInfinity。 |
| UT-015 | [CreateGame_RejectsNonFiniteThinkingTime(float.NegativeInfinity)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:39) | 创建牌局时拒绝非有限思考时间并抛出 ArgumentException。 参数：float.NegativeInfinity。 |
| UT-016 | [StartNew_HeadsUp_ButtonIsSmallBlindAndActsFirstPreflop](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:50) | 两人局按钮位支付小盲并在翻牌前先行动；另一人支付大盲。 |
| UT-017 | [StartNew_Multiway_FirstActionIsLeftOfBigBlind](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:66) | 三人首手正确收取盲注、发两张底牌，大盲左侧先行动。 |
| UT-018 | [Preflop_AllPlayersCall_BigBlindRetainsOptionAndClosesWithCheck](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:84) | 其他人跟注后大盲仍有行动机会；大盲过牌后阶段结束事件恰好一次。 |
| UT-019 | [NextStage_ResetsMinimumBetToBigBlindAndBurnsBeforeFlop](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:103) | 翻牌前烧一张、发三张；重置本轮下注与最小下注，拒绝 99、接受大盲 100。 |
| UT-020 | [Call_WithTooFewChips_CommitsStackAsAllInCall](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:126) | 剩余筹码不足跟注时投入全部余款，余额归零并标记 all-in。 |
| UT-021 | [StartNew_SkipsEliminatedPlayerAndSwitchesToHeadsUpPositions](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:144) | 跳过失去资格的玩家，不发底牌，剩余两人使用单挑位置规则。 |
| UT-022 | [StartNew_ThreeHandedToHeadsUp_PreviousBigBlindDoesNotPostBigBlindTwice](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:161) | 三人变两人的指定样例中，上手大盲不连续支付大盲。 |
| UT-023 | [StartNew_MultiwaySkipsInactiveButtonAfterElimination](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:178) | 新规则跳过不参战的按钮位置；指定样例按钮 2、小盲 3、大盲 0。 |
| UT-024 | [StartNew_PlayerBelowBigBlindDoesNotParticipate](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:194) | 筹码少于大盲的玩家不发牌、不支付盲注、不参加该手。 |
| UT-025 | [StartNew_DatabaseWriteFailureLeavesBalancesUnchangedAndCanRetry](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:207) | 低层规则测试：开手扣款故障返回 -7，余额和奖池不变；手动更换仓储后可重试。正式服务器现在遇存储异常停服。 |
| UT-026 | [JoinDuringHand_WaitsThenParticipatesInNextHandAtStableSeat](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:221) | 首手中加入固定座位，等待且无底牌；结算并开下一手后参与并获两张底牌。 |
| UT-027 | [DisconnectDuringHand_AutoActsAndCanRestoreSameSeat](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:252) | 当前玩家断线后执行自动操作；重连恢复原座位及连接状态。 |
| UT-028 | [DisconnectDuringHand_AutoChecksWhenFreeAndFoldsWhenFacingBet](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:274) | 断线时无需跟注则过牌，面对下注则弃牌；这不是等待真实计时器到期的测试。 |
| UT-029 | [AllInPlayer_RemainsEligibleAfterDisconnectAndSessionReplacement](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:299) | 全下后断线、顶号及重连不撤销 all-in，也不把玩家弃牌。 |
| UT-030 | [SettlementFailure_CanRetryWithoutDuplicateCredits](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:328) | 低层规则测试：提前结束结算失败保留奖池，手动更换仓储重试成功且守恒。正式服务器现在遇存储异常停服；全下另见 UT-146。 |
| UT-031 | [SpectatorClientView_HasNoPrivateCardsOrOperationIdentity](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:350) | 旁观投影没有私牌、行动身份或加注权。 |
| UT-032 | [StartNew_WithSeatGapUsesStablePhysicalSeatIndices](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:364) | 有空座时保留物理座号，指定一手的按钮、盲注及行动位置正确；不覆盖连续轮转。 |
| UT-033 | [RaiseRights_ShortAllInDoesNotReopenButCumulativeAmountDoes](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:383) | 状态级校验：单次不足额全下不重开加注权，累计达到完整加注额后重开。 |
| UT-034 | [Actions_ShortAllInThatDidNotReopenRejectsRaiseAndOversizedAllIn](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:401) | 操作入口拒绝未重开加注权者继续加注或超出跟注额全下。 |
| UT-035 | [Fold_IsLegalWhenCheckingIsAlsoAvailable](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:419) | 即使可以免费过牌，玩家仍可合法弃牌。 |
| UT-036 | [EarlyTermination_AwardsPotConservesChipsAndDoesNotRevealCards](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:432) | 其他玩家弃牌后结算，奖池归零、总筹码守恒，结果不暴露底牌。 |
| UT-037 | [Showdown_RevealsOnlyPlayersWhoDidNotFold](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:452) | 摊牌时仅展示未弃牌玩家的两张底牌。 |

### RoomManagerSeatTest（4 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-038 | [ActiveRoom_ObserversDoNotOccupyOrReuseSeats](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:685) | 满桌仍可加入旁观；旁观无座号，离开后的旧座号不复用；房主离开时移交成员。 |
| UT-039 | [LastMemberLeavingDissolvesRoomImmediately](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:709) | 仍有成员时保留房间，最后成员离开时解散。 |
| UT-040 | [WatchingMember_ReleasesSeatAndRestoresAsUnseatedSpectator](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:722) | 释放旁观座位后，重连回当前牌局仍为无座旁观，需主动申请下一手。 |
| UT-041 | [SpectatorDecliningReturn_RemovesFinalRoomAssociation](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/GameManagerRulesTest.cs:744) | 无座旁观的最后成员离开后，删除房间及成员关联。 |

### HoldemRulesTest（7 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-042 | [GetPositions_Multiway_ActsLeftOfBigBlindPreflop](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/HoldemRulesTest.cs:9) | 多人局计算小盲、大盲、翻牌前与翻牌后首位行动者。 |
| UT-043 | [GetPositions_Multiway_AllowsDeadButtonSeat](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/HoldemRulesTest.cs:22) | 指定按钮为空座时仍能计算多人局的盲注和行动顺序。 |
| UT-044 | [GetPositions_HeadsUp_ButtonPostsSmallBlindAndActsFirstPreflop](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/HoldemRulesTest.cs:35) | 两人局按钮兼小盲：翻牌前先行动，翻牌后由大盲先行动。 |
| UT-045 | [CanRaise_SingleShortAllIn_DoesNotReopenAction](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/HoldemRulesTest.cs:48) | 不足完整加注的单次短码全下，不重开已经行动者的加注权。 |
| UT-046 | [CanRaise_CumulativeShortAllIns_ReopenActionAtFullRaiseAmount](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/HoldemRulesTest.cs:58) | 多次短码全下累计达到完整加注增量后，重开加注权。 |
| UT-047 | [CanRaise_PlayerWhoHasNotActed_CanRaiseShortAllIn](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/HoldemRulesTest.cs:68) | 尚未行动者仍可对短码全下进行合法加注。 |
| UT-048 | [GetMinimumRaiseTotal_AddsLastFullRaiseToCurrentWager](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/HoldemRulesTest.cs:78) | 最小加注到的总额等于当前最高下注加上一次完整加注增量。 |

### LogTest（1 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-049 | [LogPath_FailedChangeKeepsExistingWriterAndReopenAppends](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/LogTest.cs:11) | 切换到非法日志路径失败时保留原写入器；重新打开原日志采用追加。 |

### PokerTest（35 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-050 | [PokerSizeTest1](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:12) | A 大于 2；同时验证不小于。 |
| UT-051 | [PokerSizeTest2](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:21) | 10 小于 K；同时验证不大于。 |
| UT-052 | [PokerSizeTest3](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:30) | 不同花色的 Q 相等；不大于也不小于。 |
| UT-053 | [PokerHandTest1](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:40) | 高牌：按 A、K、Q、10、2 排列比较等级。 |
| UT-054 | [PokerHandTest2](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:62) | 一对 A：对子后按 K、Q、10 排列踢脚。 |
| UT-055 | [PokerHandTest3](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:84) | 两对 10 和 2：较大对子、较小对子、A 踢脚。 |
| UT-056 | [PokerHandTest4](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:105) | 三条 K：三条后按 10、2 排列踢脚。 |
| UT-057 | [PokerHandTest5](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:125) | A2345 为 5 高顺子，A 作低牌。 |
| UT-058 | [PokerHandTest6](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:143) | 10JQKA 为 A 高顺子。 |
| UT-059 | [PokerHandTest7](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:161) | 8910JQ 为 Q 高顺子。 |
| UT-060 | [PokerHandTest8](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:179) | 同花：按 K、Q、10、5、2 比较。 |
| UT-061 | [PokerHandTest9](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:201) | 葫芦：三条 Q 加一对 2。 |
| UT-062 | [PokerHandTest10](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:220) | 四条 8 加 K 踢脚。 |
| UT-063 | [PokerHandTest11](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:239) | 6–10 同花顺为 10 高。 |
| UT-064 | [PokerHandTest12](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:257) | 皇家同花顺归类为 A 高同花顺。 |
| UT-065 | [PokerRankTest1](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:276) | 高牌小于一对。 |
| UT-066 | [PokerRankTest2](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:307) | 高牌先比较最高牌，A 高胜 J 高。 |
| UT-067 | [PokerRankTest3](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:338) | 一对先比对子，QQ 胜 77。 |
| UT-068 | [PokerRankTest4](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:369) | 相同一对 2：比较 A、K 后，Q 踢脚胜 J。 |
| UT-069 | [PokerRankTest5](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:400) | 两对的较大对子相同：AAJJ 胜 AA22。 |
| UT-070 | [PokerRankTest6](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:431) | 相同 QQ22：K 踢脚胜 J。 |
| UT-071 | [PokerRankTest7](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:462) | 三条先比三条点数，QQQ 胜 JJJ。 |
| UT-072 | [PokerRankTest8](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:494) | 相同 JJJ：K 踢脚相同后，10 胜 2。 |
| UT-073 | [PokerRankTest9](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:525) | 顺子比顶牌，A 高胜 K 高。 |
| UT-074 | [PokerRankTest10](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:556) | 同花前四张相同，比较第五张，3 胜 2。 |
| UT-075 | [PokerRankTest11](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:587) | 同花先比最高牌，A 高胜 K 高。 |
| UT-076 | [PokerRankTest12](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:618) | 葫芦先比三条，AAA55 胜 KKK66。 |
| UT-077 | [PokerRankTest13](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:649) | 葫芦三条相同时比对子，KKK66 胜 KKK55。 |
| UT-078 | [PokerRankTest14](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:680) | 相同葫芦等级相等。 |
| UT-079 | [PokerRankTest15](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:711) | 四条先比四条点数，AAAA 胜 KKKK。 |
| UT-080 | [PokerRankTest16](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:742) | 相同四条 K：Q 踢脚胜 6。 |
| UT-081 | [PokerRankTest17](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:773) | 相同四条和踢脚等级相等。 |
| UT-082 | [PokerRankTest18](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:804) | A 高同花顺胜 K 高同花顺。 |
| UT-083 | [PokerRankTest19](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:835) | 相同五张高牌等级相等。 |
| UT-084 | [PokerRankTest20](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PokerTest.cs:866) | 高牌 A 胜另一手最高牌 K。 |

### PotCalculatorTest（8 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-085 | [Calculate_EqualContributions_CreatesOnePot](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PotCalculatorTest.cs:14) | 投入 [100,100,100] → 单池 300，三人有资格，无退款。 |
| UT-086 | [Calculate_OneShortAllIn_CreatesMainAndSidePot](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PotCalculatorTest.cs:26) | 投入 [50,150,150] → 主池 150、边池 200，短码者只能争主池。 |
| UT-087 | [Calculate_LaterStreetBets_StayOutsideShortStacksMainPot](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PotCalculatorTest.cs:38) | 投入 [50,250,250] → 主池仍为 150，后续下注进入 400 边池。 |
| UT-088 | [Calculate_MultipleAllIns_CreatesOneLayerPerContributionLevel](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PotCalculatorTest.cs:51) | 投入 [30,80,200,200] → 三层奖池 120、150、240，逐层缩小资格集合。 |
| UT-089 | [Calculate_FoldedPlayerContributesDeadMoneyButCannotWin](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PotCalculatorTest.cs:64) | 投入 [50,150,150] 且第三人弃牌 → 仍计入其投入，但任何奖池都排除其获胜资格。 |
| UT-090 | [Calculate_UncalledChips_AreRefundedInsteadOfCreatingOnePlayerPot](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PotCalculatorTest.cs:76) | 投入 [200,120] → 竞争池 240，向第一人退回未被跟注的 80。 |
| UT-091 | [Calculate_EnumeratedSmallStates_AlwaysConservesChips](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PotCalculatorTest.cs:88) | 枚举 2–4 人、投入值 {0,1,2,5} 及合法弃牌组合；奖池加退款等于投入，获奖资格不含弃牌者。 |
| UT-092 | [GetOddChipOrder_StartsAtFirstWinnerLeftOfDealer](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/PotCalculatorTest.cs:125) | 5 座、按钮 2、胜者 [0,2,3] → 奇数筹码顺序 [3,0,2]。 |

### RoomDepartureTest（11 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-093 | [QueueAndCancel_PreserveTurnCardsChipsMembershipAndActionRights](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:77) | 重复预约保持幂等，不改行动人、底牌、筹码、房主或成员；仍能跟注，取消后恢复下一手资格。 |
| UT-094 | [FoldedAndAllInParticipants_StillWaitForSettlement(false)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:105) | 本手参与者即使已弃牌或全下也只能预约离开，结算前保留成员身份。 参数：false。 |
| UT-095 | [FoldedAndAllInParticipants_StillWaitForSettlement(true)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:105) | 本手参与者即使已弃牌或全下也只能预约离开，结算前保留成员身份。 参数：true。 |
| UT-096 | [WaitingJoinerAndUnseatedSpectator_LeaveImmediatelyDuringActiveHand](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:120) | 新加入等待者及无座旁观者在牌局中立即离房，清理关联而不中止他人的牌局。 |
| UT-097 | [Settlement_CreditsAllInLeaverBeforeRemovingAndTransferringOwner](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:139) | 先向全下预约离开者结算到账，结果事件保留成员，下一手准备时再移除并移交房主；只通知一次，不向离开者发下一手。 |
| UT-098 | [DisconnectAndReconnect_PreservePendingDepartureAndDoNotRestoreFoldedHand](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:170) | 预约后断线再重连仍保留预约，已发生的弃牌不撤销。 |
| UT-099 | [OfflinePendingPlayer_IsRemovedAfterSettlementUsingAccountPersistence](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:183) | 预约者持续离线，查看结束后仍通过账户存储清除 LastRoomId 并移除成员。 |
| UT-100 | [StorageFailure_KeepsPendingPlayerOutOfNextHandUntilRetrySucceeds](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:195) | 离房落库失败时保留预约和关联、排除下一手资格；存储恢复后可完成离房。 |
| UT-101 | [EveryoneQueues_RoomExistsUntilSettlementAndThenDissolves](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:212) | 全员预约仍保留当前房间到结算完成；查看期结束执行预约时全部移除、房间解散、对账一致。 |
| UT-102 | [InvalidRoomOrMember_CannotChangeMembershipOrPendingState](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:228) | 无效房间或非成员请求返回错误，不改变任何预约状态。 |
| UT-103 | [Projection_RoundTripsDepartureFlagWithoutDisclosingOtherHoleCards](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/RoomDepartureTest.cs:236) | JSON 往返保留预约标记；只包含自己的 Hole，不泄漏其他底牌、AccountId 或 Deck。 |

### TableParticipationTest（21 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-104 | [CreateJoinReadyStart_DealsOnlyTheTwoPlayers](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:227) | 建房、另一玩家加入并准备、房主开局；只给两名参赛者发牌并正确收盲注。 |
| UT-105 | [FullRoom_AllowsUnlimitedUnseatedObserversButRejectsParticipation](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:237) | 满桌接收多个无座旁观者；拒绝满额参战、低余额参战；JSON 不含其他底牌或服务器秘密。 |
| UT-106 | [Observer_ContinuesWatchingAcrossThreeHandsWithoutAutomaticEntry](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:257) | 明确继续旁观后跨三手保持无座、无底牌，不自动入座。 |
| UT-107 | [JoinNextHand_CanCancelAndAllocatesOnlyWhenTheNextHandBegins](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:273) | 申请和取消都不占座；重新申请后仅在下一手开始分配新座号并获得两张底牌。 |
| UT-108 | [Reservations_PreventOverbookingAndGuardCancellingSitOut](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:293) | 转旁观释放的下一手名额可预留；不超订，取消转旁观不能抢占已预留名额。 |
| UT-109 | [CancelDeparture_CannotTakeAPlaceAlreadyReservedByAnObserver](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:308) | 预约离房者取消时同样检查预留名额；申请者取消后允许取消离房。 |
| UT-110 | [WaitingApplicant_LeavesImmediatelyAndReleasesReservation](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:321) | 申请参战的旁观者立即离房，清理预留及账户关联，不改变当前手或奖池。 |
| UT-111 | [SitOut_PreservesFoldedOrAllInRightsUntilSettlement(false)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:337) | 预约转旁观不改变本手底牌和行动权；弃牌/all-in 后仍等到账务结算，再释放座位。 参数：false。 |
| UT-112 | [SitOut_PreservesFoldedOrAllInRightsUntilSettlement(true)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:337) | 预约转旁观不改变本手底牌和行动权；弃牌/all-in 后仍等到账务结算，再释放座位。 参数：true。 |
| UT-113 | [SitOut_DisconnectAndReconnectKeepsIntentAndCurrentHandRights](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:355) | 预约转旁观后断线重连保留原预约和本手底牌。 |
| UT-114 | [DepartedSeat_IsNotReusedAndSpectatorReentryGetsANewNumber](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:365) | 转旁观者释放旧座位；新成员和原玩家重新参战时得到递增的新号。 |
| UT-115 | [CompactSeats_OnlyAboveHalfEmptyAndOnlyBeforeANewHand(4, false)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:385) | 2/4 空座不压缩；3/5 空座在新手前压缩；本手进行中不能重新开手或重编号。 参数：4, false。 |
| UT-116 | [CompactSeats_OnlyAboveHalfEmptyAndOnlyBeforeANewHand(5, true)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:385) | 2/4 空座不压缩；3/5 空座在新手前压缩；本手进行中不能重新开手或重编号。 参数：5, true。 |
| UT-117 | [SparseSeats_BlindsRotatePastGapsAcrossMultipleHands](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:400) | 座位 0、2、4 连续六手：大盲依次 4、0、2、4、0、2，所有按钮/盲注跳过空座。 |
| UT-118 | [OldTurnRequest_IsRejectedWhenSamePlayerActsOnTheNextStreet(PlayerOperationType.Check)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:426) | 真实超时回调后推进至下一下注街，同一玩家再行动时拒绝旧请求，奖池与行动编号不变。 参数：PlayerOperationType.Check。 |
| UT-119 | [OldTurnRequest_IsRejectedWhenSamePlayerActsOnTheNextStreet(PlayerOperationType.Call)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:426) | 真实超时回调后推进至下一下注街，同一玩家再行动时拒绝旧请求，奖池与行动编号不变。 参数：PlayerOperationType.Call。 |
| UT-120 | [OldTurnRequest_IsRejectedWhenSamePlayerActsOnTheNextStreet(PlayerOperationType.Fold)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:426) | 真实超时回调后推进至下一下注街，同一玩家再行动时拒绝旧请求，奖池与行动编号不变。 参数：PlayerOperationType.Fold。 |
| UT-121 | [OldTurnRequest_IsRejectedWhenSamePlayerActsOnTheNextStreet(PlayerOperationType.AllIn)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:426) | 真实超时回调后推进至下一下注街，同一玩家再行动时拒绝旧请求，奖池与行动编号不变。 参数：PlayerOperationType.AllIn。 |
| UT-122 | [DuplicateAndPreviousHandOperations_AreRejectedWithoutChangingBalances](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:446) | 同一有效请求仅执行一次；即使下一手 TurnId 相同，旧 HandId 仍被拒绝；客户端 JSON 保留编号。 |
| UT-123 | [OldParticipationIntent_CannotUndoANewerChoice](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:468) | 参战状态请求含手牌及修订号；旧取消不能覆盖新的申请。 |
| UT-124 | [SpectatorSettlement_PreservesPublicCardsAndKeepsFoldedCardsPrivate](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:479) | 旁观结算保留已公开的底牌，弃牌／未公开牌仍隐藏；复制结果不修改原始参赛者视图。 |

### UIInteractionTest（原有 21 项；新增一项见 UT-157）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-125 | [BetInput_CorrectionUpdatesBothDisplayAndSubmittedTotal("1", 200u)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:50) | 输入矫正后的显示值与内部待提交总额一致；此用例不发送网络请求。 参数："1", 200u。 |
| UT-126 | [BetInput_CorrectionUpdatesBothDisplayAndSubmittedTotal("9000", 1000u)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:50) | 输入矫正后的显示值与内部待提交总额一致；此用例不发送网络请求。 参数："9000", 1000u。 |
| UT-127 | [BetInput_CorrectionUpdatesBothDisplayAndSubmittedTotal("350", 350u)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:50) | 输入矫正后的显示值与内部待提交总额一致；此用例不发送网络请求。 参数："350", 350u。 |
| UT-128 | [BetInput_CorrectionUpdatesBothDisplayAndSubmittedTotal("invalid", 200u)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:50) | 输入矫正后的显示值与内部待提交总额一致；此用例不发送网络请求。 参数："invalid", 200u。 |
| UT-129 | [BetInput_CorrectionUpdatesBothDisplayAndSubmittedTotal("18446744073709551615", 1000u)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:50) | 输入矫正后的显示值与内部待提交总额一致；此用例不发送网络请求。 参数："18446744073709551615", 1000u。 |
| UT-130 | [BetInput_ShortAllInClampsToAvailableTotalIncludingPriorBet](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:63) | 短码全下上限包含本轮已下注部分，示例矫正到 150，保留合法下注按钮。 |
| UT-131 | [BetInput_UnrelatedSyncPreservesEditingAndTurnChangeResetsIt](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:79) | 无关同步保留编辑值 375；行动状态改变后重置为合法值 400。 |
| UT-132 | [Actions_SpectatorsAndClosedRaiseRightsCannotUseBetInput](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:95) | 加注权未重开时隐藏加注及不合法全下；旁观者没有操作按钮。 |
| UT-133 | [GamePanel_ResizesAndHidesVacantSeatsAcrossRooms](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:111) | 牌桌可容纳八个座位并隐藏空座；换成两人房后仅显示两名玩家。 |
| UT-134 | [PlayerCountdown_DuplicateSyncDoesNotRestartElapsedTime](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:128) | 重复同步不重置已流逝倒计时；切换新的计时条件时重置。 |
| UT-135 | [RoundResult_HiddenOrFoldedHandsNeverDisplayAnInferredPattern](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:145) | 已公开赢家显示牌型，弃牌及未公开底牌者不推算展示牌型。 |
| UT-136 | [ResolutionDropdown_FirstOpenSelectsActualSizeInDeduplicatedList](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:166) | 首次打开设置选择实际窗口分辨率，选项去重。 |
| UT-137 | [GameResults_SortsUnsignedChipTotalsWithoutOverflow](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:176) | 按 uint 筹码大小正确排序，避免转成有符号整数后溢出。 |
| UT-138 | [RoomList_PeriodicRefreshPreservesExistingJoinButtons](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:192) | 相同房间 ID 刷新后复用同一条目，保持按钮对象及焦点；不验证真实网络点击。 |
| UT-139 | [DepartureControls_UseServerStateAndKeepBettingAvailableWhileQueued](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:204) | 预约时显示取消与提示、仍可下注；请求处理中禁用按钮，等待者显示立即离房。 |
| UT-140 | [HidingGamePanel_StopsSeatTimersAndClearsInterHandCountdown](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:260) | 隐藏牌桌时停止所有座位计时，并隐藏局间倒计时。 |
| UT-141 | [SpectatorControls_ClearPrivateCardsAndReflectAvailableSeats(false)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:272) | 真实 GamePanel 从玩家切到旁观后清空私牌、隐藏下注控件；满额/有位分别显示正确按钮。 参数：false。 |
| UT-142 | [SpectatorControls_ClearPrivateCardsAndReflectAvailableSeats(true)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:272) | 真实 GamePanel 从玩家切到旁观后清空私牌、隐藏下注控件；满额/有位分别显示正确按钮。 参数：true。 |
| UT-143 | [PendingParticipation_UIAllowsContinuingToWatchAndClearsOnDisable](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:300) | 已申请时禁用重复参战按钮，继续旁观可取消；等待响应禁用操作，隐藏界面清理等待状态。 |
| UT-144 | [SitOutControls_KeepCurrentBettingRightsAndDisplayCancellation](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:320) | 预约转旁观显示取消及结算后提示，同时保留本手跟注操作。 |
| UT-145 | [FullActiveRoom_StillHasAnEnabledLobbyEntryForSpectators](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:334) | 满桌在大厅仍有可用加入条目，参战人数与旁观人数分开显示。 |

## 本次新增停服用例（12 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-146 | [SettlementFailure_StopsEveryTableWithoutRetryOrRefund("allin")](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:78) | 真实 SQLite 结算写入失败；全下自动河牌入口触发一次停服，全部桌子停止，无结算事件/重试/退款；重启退款守恒且不重复。 |
| UT-147 | [SettlementFailure_StopsEveryTableWithoutRetryOrRefund("showdown")](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:78) | 普通摊牌保存失败触发停服；检查另一桌计时、账本、事件和重启恢复。 |
| UT-148 | [SettlementFailure_StopsEveryTableWithoutRetryOrRefund("fold")](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:78) | 弃牌提前结束保存失败触发停服；检查另一桌计时、账本、事件和重启恢复。 |
| UT-149 | [AuthenticationStorageFailure_IsReportedEvenWhenAccountServiceCatchesIt("registration")](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:129) | 注册存储报错虽被账户服务转换为错误码，统一仓储仍报告一次致命异常并阻止后续写入。 |
| UT-150 | [AuthenticationStorageFailure_IsReportedEvenWhenAccountServiceCatchesIt("session")](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:129) | 会话保存失败仍触发一次致命异常，不受账户服务 catch 影响。 |
| UT-151 | [DebitStorageFailure_StopsPlayWithoutAdditionalLedgerWrites(false)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:144) | 本手跟注扣款失败时停止牌局，余额及账本条数不变。 |
| UT-152 | [DebitStorageFailure_StopsPlayWithoutAdditionalLedgerWrites(true)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:144) | 新一手收取盲注失败时停止牌局，不在故障期间追加退款写入。 |
| UT-153 | [RejectedSettlement_IsFatalInsteadOfLeavingAutoRunoutStuck](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:167) | 仓储拒绝无效手牌结算时触发致命异常，避免自动流程只记录错误后悬挂。 |
| UT-154 | [RoomAssociationWriteFailure_StopsActiveHand](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:177) | 保存上次房间关联失败会停止正在进行的手牌及计时。 |
| UT-155 | [ExpectedRejections_DoNotStopStorageOrServer](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:188) | 错误登录、无效名称、重名、筹码不足属于正常拒绝，不停服，后续正常写入仍成功。 |
| UT-156 | [DatabaseInitializationFailure_IsFatalAndReportedOnce](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/FatalStorageTest.cs:202) | 数据库路径实际为目录导致初始化失败；只报告一次，后续访问直接阻止。 |
| UT-157 | [ServerShutdownNotice_ShowsStorageReasonWithoutRecoveryPromises](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:350) | 正式 MsgBox prefab 显示保存失败原因、单个“知道了”按钮，不含普通网络提示或保存成功承诺。 |

## 既有测试资料与历史记录

以下保留上一轮的用例/执行记录，描述旧行为之处仅用于追溯。当前行为、最新测试结果和新增 spectators 场景以本文上半部分与 SpectatorLifecycle.md 为准。

### 原有独立进程联机场景

驱动：[PokerIntegrationRunner.cs](D:/GitHubRepos/Poker/Assets/Scripts/Network/Diagnostics/PokerIntegrationRunner.cs:15)。一个 Windows server 加三个 Windows client，使用独立测试数据库。客户端响应先走正式 UI 处理器，再由驱动记录状态；操作请求由驱动生成。无图形模式的成功不等于所有视觉或鼠标命中都通过。

| 编号 | 场景 / 检查点 | 已有结果 |
| --- | --- | --- |
| NET-01 | 默认流程：三个账户注册认证，A 建房、B 入房准备，A 开始首手。 | 历史通过；默认流程报告 `Logs/UIReview/integration-970d87bd/`。 |
| NET-02 | C 在首手开始后加入：WaitingForNextHand、0 张底牌。 | 默认与 departures 均通过。 |
| NET-03 | 首手由脚本执行跟注、过牌、翻牌后下注，经过河牌并结算。 | 客户端均观察到下注、河牌及结算。 |
| NET-04 | 收到局间倒计时；第二手 C 变成 Playing 并获两张底牌。 | 三号客户端两个状态标记均为 true。 |
| NET-05 | 第二手通过弃牌结束，各客户端完成两手并离房。 | 最终 departures 三个客户端均 Rounds=2、LeaveCompleted=true。 |
| NET-06 | 服务器最终对账：三个账户共 30,000，账本净额相同、活动投入和活动手牌为 0。 | 默认及 departures 均一致。 |
| NET-07 | departures：A 预约离开、取消预约，本手继续操作。 | QueueAndCancel=true。 |
| NET-08 | departures：C 作为等待者立即离房，再加入当前手等待。 | WaitingPlayerLeft=true，并最终下一手参战。 |
| NET-09 | departures：C 在两手之间离房再加入。 | BetweenHandsLeft=true。 |
| NET-10 | departures：A 再次预约，断线返回标题、重连并恢复原预约。 | DisconnectedToTitle=true、PendingRecovered=true。 |
| NET-11 | departures：预约者完成结算后自动离房；三人的 LastRoomId 都清空。 | 客户端离房完成，服务器断言全部关联为空。 |
| NET-12 | departures：退出请求故意填入错误玩家名，服务端仍仅处理认证连接所属玩家。 | 通过；不是对所有伪造网络消息的完整攻击矩阵。 |
| NET-13 | departures：正式构建内 GamePanel 已显示，退出按钮及文字与服务器状态一致。 | 最终构建通过；覆盖“本手后离开 / 取消离开 / 离开房间”。 |

最新 departures 四份通过报告位于 `Logs/RoomLifecycle/integration-9fa9b089/`，构建为 `build-f3a84a1982`。默认场景的 970d87bd 是较早构建记录；其共同业务链也被后续 departures 覆盖。失败调试轮次保留在 Logs，但不能算作通过。

迁移阶段另记录过第二个服务器进程重新打开同一数据库并对账一致，详见 [CashGameClientAcceptance.md](D:/GitHubRepos/Poker/Docs/CashGameClientAcceptance.md)。这是历史补充验证，不是当前 PokerIntegrationRunner 内独立命名的第三套场景，本次未重跑服务器重启。

## 5. 既有客户端人工验收清单（20 项）

来源：[CashGameClientAcceptance.md](D:/GitHubRepos/Poker/Docs/CashGameClientAcceptance.md)。下面保留原文 7 + 5 + 8 项的业务范围，补充其证据层级。没有全程可见窗口点击记录的项目，不标成“人工全程已通过”。

| 编号 | 操作与预期 | 当前覆盖证据 |
| --- | --- | --- |
| AC-01 | 启动 1 个 server 与 A/B/C 三个 client。 | 独立进程联机通过。 |
| AC-02 | 标题进入登录，三人注册各得 10,000；错误密码与重复注册被拒绝。 | 注册有联机覆盖；错误密码及同名限制有 NUnit 覆盖；页面有历史检查。 |
| AC-03 | A 建房并设置盲注/人数；界面没有旧“初始筹码”设置。 | 建房联机覆盖；设置外观历史检查，字段所有边界未完整覆盖。 |
| AC-04 | B 入房准备，A 开手，筹码来自账户。 | 联机覆盖。 |
| AC-05 | C 中途加入，只等待，不看私牌，不操作。 | NUnit、联机及本次 UI 接线检查。 |
| AC-06 | 当前手结束有 10 秒倒计时；C 下一手两张底牌并参战，无额外入桌补盲。 | 联机覆盖状态、底牌及倒计时；指定盲注扣款有 NUnit 样例。 |
| AC-07 | 倒计时有人离开，人数不足两人回房间页，条件满足后房主重新开始。 | 联机覆盖倒计时离房再加入；不足两人的完整 UI 分支尚缺专项自动化。 |
| AC-08 | 当前玩家断线：免费时过牌，需跟注时弃牌。 | NUnit 覆盖两分支；不等于真实超时/网络竞争完整覆盖。 |
| AC-09 | 本手结束前重新登录，恢复原座位，不能撤销已完成操作。 | NUnit 覆盖，departures 覆盖预约重连路径。 |
| AC-10 | all-in 后掉线不弃牌，保留主池/边池资格。 | NUnit 覆盖全下状态不丢失；不同边池最终获奖仍缺组合验收。 |
| AC-11 | 同账户在第二个客户端登录，旧连接被替换；旧消息与断线回调不影响新连接。 | NUnit 覆盖会话替换及迟到断线；旧操作跨行动轮风险仍在 review 中。 |
| AC-12 | 结算时仍断线者释放座位转旁观；再登录提示返回或离开。 | 座位/恢复有 NUnit；完整提示交互未作为本次独立验收执行。 |
| AC-13 | 关闭再开客户端，保存令牌自动登录。 | 令牌服务 NUnit；真实应用重启后的本地/浏览器存储链未在本次重测。 |
| AC-14 | 自动登录轮换令牌；旧/过期令牌无效并要求密码。 | 令牌轮换、过期有 NUnit；UI 组合分支尚无全套自动化。 |
| AC-15 | 设置只读用户名；登出返回标题并清除保存状态，再进入需登录。 | 历史 UI 检查及编辑器联机登出检查。 |
| AC-16 | 房主离开后房间保留、移交房主。 | 房间与离房 NUnit 覆盖。 |
| AC-17 | 最后成员离开解散房间；再次登录不恢复失效房间。 | 解散及清理关联有 NUnit/联机；再次登录提示分支无本次点击记录。 |
| AC-18 | 在 A 房赢得筹码，离开后进入 B 房直接沿用余额。 | 账本持久化覆盖；真实双房间 UI 迁移尚缺独立场景。 |
| AC-19 | 原桌玩家筹码不足大盲时提示旁观或离开，不发下一手。 | 资格与旁观数据层有 NUnit；提示选择完整点击流程未在本次重测。注意大厅低余额直接加入会被拒绝。 |
| AC-20 | 服务重启后账户/密码/会话/筹码持久化，清理失效 LastRoomId。 | 仓储持久化、崩溃退款有 NUnit；历史重启对账记录，不是最新构建全链重测。 |

迁移计划第 7 节另列过 16 个目标矩阵条目。注册、账户持久化、会话替换、单房间关联、资格门槛、中途加入、下一手参战、断线、恢复、释放座位、倒计时离房、房主移交及守恒均映射到上表和 NUnit。以下三项不能从已有样例推断为完整覆盖：**所有位置前后的加入/离开、三人与两人之间任意切换、全部重放与伪造请求**；尤其空座多手轮转和迟到操作已存在确认的问题。

## 6. 已做过的 UI 与编辑器专项检查

这部分是既有工作记录中的定向人工/脚本检查，不是新增 NUnit 用例，也不是此次重新逐项执行。

| 编号 | 检查 | 结果与边界 |
| --- | --- | --- |
| UI-M01 | 标题、登录、设置、大厅、房间、牌桌、结算、确认弹窗的排版。 | 历史 Play Mode 预览；部分画面使用本地示例数据。 |
| UI-M02 | 1920×1080、1600×1000 布局、牌面、操作区与圆角裁切。 | 两种尺寸已有预览；不代表所有比例。 |
| UI-M03 | 按钮射线命中、悬停/按下/禁用状态，禁用按钮不响应。 | 历史 EventSystem 检查通过。 |
| UI-M04 | 输入框、16 人列表滚动、弹窗关闭与未连接时返回。 | 历史 Play Mode 检查；详情见 UIVisualRefresh。 |
| UI-M05 | 弹窗回调仅一次、关闭后键盘输入恢复、默认分辨率选择。 | 历史 UIReview 检查；分辨率另有 NUnit。 |
| UI-M06 | 新纹理版本：编辑器 server+client 与外部 Windows client，两手牌中真实点击 4 次操作按钮。 | `Logs/UITextureRefresh/live-smoke-v2/client-1.pass` 历史通过；不是三个可见窗口从大厅点击加入的完整记录。 |
| UI-M07 | 联机后登出，连接关闭并回标题页。 | UITextureAssets 历史验证记录。 |
| UI-M08 | 离房按钮、预约提示、确认窗，两个尺寸下检查并验证射线最上层命中。 | RoomLifecycle 历史通过；最终构建也断言离房控件存在。 |
| UI-M09 | Game.unity 与 21 个 UI/Manager prefab 的 Missing Script、损坏序列化引用。 | 历史检查通过；本次只重新读取大厅加入相关 prefab。 |
| UI-M10 | 启动连接后同帧取消的压力检查。 | 页面返回，但历史出现过 Mirror SimpleWeb 后台空引用；依赖竞态未修，不能标作干净通过。 |

原记录：[LogicAndUIReview.md](D:/GitHubRepos/Poker/Docs/LogicAndUIReview.md)、[UIVisualRefresh.md](D:/GitHubRepos/Poker/Docs/UIVisualRefresh.md)、[UITextureAssets.md](D:/GitHubRepos/Poker/Docs/UITextureAssets.md)、[RoomLifecycle.md](D:/GitHubRepos/Poker/Docs/RoomLifecycle.md)。早期文档中的 85、88、106 是当时套件规模，不应与现在 119 相加。

## 7. 旧手动场景脚本（7 个）

它们没有 NUnit 断言，当前场景和 prefab 中未查到挂载。直接运行 Game.unity 或点击 Test Runner 不会执行这些 Start。此处按脚本列出所有现存测试入口，并标注可用性。

| 脚本 | 现有入口与用途 | 当前状态 |
| --- | --- | --- |
| [GameManagerTester](D:/GitHubRepos/Poker/Assets/Scripts/Tests/GameManagerTester.cs:18) | Test：六人开局后连续下注、弃牌，事件驱动后续阶段。 | **过时**：new GameManager() 没有账户仓储，CreateGame 返回 -7；随后访问空牌局会失败。不能作为当前规则验收。 |
| [GamePanelTester](D:/GitHubRepos/Poker/Assets/Scripts/Tests/GamePanelTester.cs:15) | Start：显示 GamePanel。 | 原 Test 调用及示例驱动已注释，只能打开空面板，不能证明牌局链路。 |
| [JsonTester](D:/GitHubRepos/Poker/Assets/Scripts/Tests/JsonTester.cs:15) | 8 个方法：TestRoomInfoSerialize / Deserialize；TestRoomListSerialize / Deserialize；TestUnionSerialize / Deserialize；TestRoundResultSerialize / Deserialize。 | Start 只调用 TestRoundResultDeserialize；仅输出/解析，无断言。TestUnionDeserialize 输入是空字符串，直接调用会解析失败；旧样例不覆盖当前完整协议字段。 |
| [LogTester](D:/GitHubRepos/Poker/Assets/Scripts/Tests/LogTester.cs:17) | Test：写一条 Info “Hello”。 | 日志冒烟，无自动判定；异常恢复由 LogTest 覆盖。 |
| [MsgBoxTester](D:/GitHubRepos/Poker/Assets/Scripts/Tests/MsgBoxTester.cs:18) | Test：创建长文本双按钮且可关闭弹窗，以及仅确认且不可关闭弹窗；打印确认/取消回调。 | 手动展示两种配置，无自动断言。 |
| [PokerTester](D:/GitHubRepos/Poker/Assets/Scripts/Tests/PokerTester.cs:18) | Test：随机 1,000 局，每局五张公牌加双方各两张底牌，打印七选五和比较结果。 | 没有独立参考答案，不能算 1,000 个通过的牌型测试。 |
| [RoundResultMsgBoxTester](D:/GitHubRepos/Poker/Assets/Scripts/Tests/RoundResultMsgBoxTester.cs:22) | Test：每 10 秒生成四名玩家的结算预览。 | 示例只有四张公牌；无限循环没有生命周期取消，属于旧预览脚本，不是规则测试。 |

## 8. 上次小规模 review 的临时探针

下列探针通过 Unity 执行临时代码，不属于已提交的固定回归套件。详情与复现条件见 [SmallRulesReview.md](D:/GitHubRepos/Poker/Docs/SmallRulesReview.md)。

| 编号 | 输入与检查 | 上次结果 |
| --- | --- | --- |
| RV-01 | 8 座仅 3 人，真实 GameManager 连续开始、弃牌结束、结算 4 手，观察盲注轮转。 | 原来失败；本轮已修并补六手空座轮转回归。 |
| RV-02 | 3 人全下、奖池 3,000，River 的 Auto 路径注入结算存储失败。 | 原探针失败；现已按存储异常停服策略修复，新增测试覆盖全下、普通摊牌和弃牌结算失败。 |
| RV-03 | 保留翻牌前的大盲 Check；先触发真实超时回调并推进到翻牌，再交付旧 Check。 | 原来失败；本轮已加入 HandId/TurnId 校验及固定回归。 |
| RV-04 | 2,000 组固定种子七张牌，加 5 组定向样例；独立参考算法对照牌型及相邻结果大小。 | 2,005 组一致；不是穷举证明，尚未固化为 NUnit。 |
| RV-05 | 抽取 500 次 RandomDeck，检查样本间整副牌排列重复。 | 样本排列未重复；不能证明随机均匀性或不可预测性。 |

## 后续缺口与运行方式

仍待处理：更多真实网络延迟/同帧操作竞争、复杂多边池逐玩家精确分配、固定七选五边界、长期多房间压力，以及当前版本的 WebGL/Linux 实机运行。上述历史资料中 85/88/106/119 均是不同阶段的套件规模，不能相加。

复跑：在 Unity 2022.3.52f1c1 Test Runner 中执行 EditMode / Assembly-CSharp-Editor，应得到当前 218 项。独立进程场景使用相同 run/output 参数启动一个 server 与 client 0/1/2，选择 `-pokerIntegrationScenario spectators` 或 `departures`；默认不指定场景参数。使用独立账户库并备份恢复本机登录偏好。详见 SpectatorLifecycle.md。

## 本轮提示交互新增用例（18 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-158 | [AllInConfirmation_IncludesPriorStageBetAndShortCalls](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:370) / AllIn | 全下按钮必须确认。 |
| UT-159 | [AllInConfirmation_IncludesPriorStageBetAndShortCalls](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:370) / Call / 刚好耗尽 | 跟注刚好耗尽剩余筹码须确认。 |
| UT-160 | [AllInConfirmation_IncludesPriorStageBetAndShortCalls](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:370) / Call / 短码 | 不足额跟注耗尽筹码须确认。 |
| UT-161 | [AllInConfirmation_IncludesPriorStageBetAndShortCalls](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:370) / Call / 有剩余 | 普通跟注不要求全下确认。 |
| UT-162 | [AllInConfirmation_IncludesPriorStageBetAndShortCalls](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:370) / Bet / 全部 | 下注总额包含本阶段已投入，全部可用筹码须确认。 |
| UT-163 | [AllInConfirmation_IncludesPriorStageBetAndShortCalls](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:370) / Bet / 有剩余 | 未耗尽筹码的下注不要求全下确认。 |
| UT-164 | [AllInConfirmation_IncludesPriorStageBetAndShortCalls](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:370) / Fold | 弃牌不使用全下确认。 |
| UT-165 | [BetInput_CorrectionExplainsReasonAndNewTurnClearsIt](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:389) / 1 | 低于最低金额时解释修正，轮次变化后清除。 |
| UT-166 | [BetInput_CorrectionExplainsReasonAndNewTurnClearsIt](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:389) / 9000 | 超出可用金额时解释全下上限。 |
| UT-167 | [BetInput_CorrectionExplainsReasonAndNewTurnClearsIt](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:389) / invalid | 格式错误时解释恢复金额。 |
| UT-168 | [RoomInput_InvalidValueExplainsCorrection](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:408) / 小盲 0 | 小盲输入无效时提示恢复原值。 |
| UT-169 | [RoomInput_InvalidValueExplainsCorrection](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:408) / 思考时间 NaN | 思考时间输入无效时提示恢复原值。 |
| UT-170 | [RoomInput_InvalidValueExplainsCorrection](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:408) / 人数 1 | 人数输入无效时提示恢复原值。 |
| UT-171 | [InsufficientPlayers_ExplainsCorrectDestination](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:420) / 参战玩家 | 人数不足提示说明返回房间等待。 |
| UT-172 | [InsufficientPlayers_ExplainsCorrectDestination](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:420) / 旁观玩家 | 人数不足提示说明返回房间列表。 |
| UT-173 | [SpectatorIntro_ExplainsExplicitEntryAndSeatLimit](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:430) | 首次旁观说明主动申请和名额限制。 |
| UT-174 | [RiskConfirmation_DefaultFocusIsCancel](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:440) | 风险确认默认键盘焦点位于取消按钮。 |
| UT-175 | [TransitionMessages_RoundTripExplicitReasonThroughMirror](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:466) | 房间同步和离开响应经 Mirror 序列化后保留原因。 |

实际联机与视觉检查见 [UI 提示说明](D:/GitHubRepos/Poker/Docs/UIFeedback.md)。

### UINavigationTest（28 项，原有 18 项列于此处，新增 10 项见文末）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-176 | [Push_KeepsCoveredPanelsActiveAndBlocksInputThroughThem](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:82) | Push 保留底层 active，同时阻止输入穿透。 |
| UT-177 | [PopMiddle_PreservesTopAndReturnsToRemainingPredecessor](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:96) | 移除中间 B 保留 C，关闭 C 后回到 A。 |
| UT-178 | [DuplicatePush_ReordersExistingInstanceWithoutDuplicatingHistory](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:112) | 重复 Push 复用实例，层级与渲染顺序一致。 |
| UT-179 | [RenderLayers_KeepSystemDialogAboveNewMiddlePanel](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:122) | System 弹窗保持在新普通面板上方。 |
| UT-180 | [DirectHide_RemovesHistoryAndCanBeReopened](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:132) | 直接 Hide 移除层级，重新 Show 可以重新加入。 |
| UT-181 | [DestroyMiddle_RemovesHistoryWithoutClosingTop](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:144) | 销毁中间项清理缓存与层级，不关闭顶层。 |
| UT-182 | [EmptyAndRepeatedPop_AreSafe](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:155) | 空容器、未知 identifier 和重复 Pop 安全返回。 |
| UT-183 | [SettingsBack_ReturnsToActualOriginRegardlessOfCachedAccount](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:167) / false | 即使保留登录资料，也返回实际打开设置的页面。 |
| UT-184 | [SettingsBack_ReturnsToActualOriginRegardlessOfCachedAccount](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:167) / true | 即使保留登录资料，也返回实际打开设置的页面。 |
| UT-185 | [RemovingSettingsOrigin_DoesNotReopenRemovedLobby](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:180) | 来源大厅被移除后，设置返回不会复活大厅。 |
| UT-186 | [ClosingLegacyResult_DoesNotNavigateOrLeaveRoom](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:191) | 旧结果框关闭保留当前房间与服务器关联。 |
| UT-187 | [Reset_DismissesModalWithoutInvokingConfirmationAndRetainsOnlyNewRoot](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:204) | 重置只保留新根面板，不触发已关闭提示的确认回调。 |
| UT-188 | [DisconnectFromSettings_ClearsInvalidReturnHistoryImmediately](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:217) | 设置期间断线立即清空无效返回路径。 |
| UT-189 | [RoomSync_RejectsDisconnectedNonmemberAndUnrelatedRoom](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:232) / false, true, 42 | 拒绝断线、非成员及不匹配房间同步。 |
| UT-190 | [RoomSync_RejectsDisconnectedNonmemberAndUnrelatedRoom](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:232) / true, false, 42 | 拒绝断线、非成员及不匹配房间同步。 |
| UT-191 | [RoomSync_RejectsDisconnectedNonmemberAndUnrelatedRoom](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:232) / true, true, 99 | 拒绝断线、非成员及不匹配房间同步。 |
| UT-192 | [StaleJoinAndStartResponses_DoNotOpenRoomOrGame](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:246) | 无对应请求的成功入房或开局响应不打开页面。 |
| UT-193 | [MatchingRoomRefresh_DoesNotCloseSettingsOrReorderIt](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:258) | 当前房间内容刷新不关闭或盖住已有设置页。 |

### NetworkConfigurationTest（25 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-194 | [Client_ParsesExplicitEndpointAndTimeout](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:18) | 解析域名或 IP、端口、加密开关与连接超时。 |
| UT-195 | [Client_ParsesEncryptedDomainAndIPv6](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:26) | 生成 WSS 域名与 IPv6 的正确 URI。 |
| UT-196 | [Client_RejectsHostContainingEndpointParts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:39) / 带 https 协议 | 拒绝混入协议、端口、路径等内容的 serverHost。 |
| UT-197 | [Client_RejectsHostContainingEndpointParts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:39) / 带端口 | 拒绝混入协议、端口、路径等内容的 serverHost。 |
| UT-198 | [Client_RejectsHostContainingEndpointParts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:39) / 带路径 | 拒绝混入协议、端口、路径等内容的 serverHost。 |
| UT-199 | [Client_RejectsHostContainingEndpointParts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:39) / 带用户名 | 拒绝混入协议、端口、路径等内容的 serverHost。 |
| UT-200 | [Client_RejectsHostContainingEndpointParts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:39) / 含空格 | 拒绝混入协议、端口、路径等内容的 serverHost。 |
| UT-201 | [Client_RejectsHostContainingEndpointParts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:39) / 空字符串 | 拒绝混入协议、端口、路径等内容的 serverHost。 |
| UT-202 | [BothSides_RejectInvalidPorts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:49) / 0 | 两端均拒绝无效端口。 |
| UT-203 | [BothSides_RejectInvalidPorts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:49) / 65536 | 两端均拒绝无效端口。 |
| UT-204 | [BothSides_RejectInvalidPorts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:49) / -1 | 两端均拒绝无效端口。 |
| UT-205 | [BothSides_RejectInvalidPorts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:49) / 1.5 | 两端均拒绝无效端口。 |
| UT-206 | [BothSides_RejectInvalidPorts](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:49) / 字符串 | 两端均拒绝无效端口。 |
| UT-207 | [Client_RejectsInvalidTimeout](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:58) / 0 | 拒绝无效连接超时。 |
| UT-208 | [Client_RejectsInvalidTimeout](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:58) / 301 | 拒绝无效连接超时。 |
| UT-209 | [Client_RejectsInvalidTimeout](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:58) / null | 拒绝无效连接超时。 |
| UT-210 | [Client_RequiresExplicitBooleanSslAndAllConnectionFields](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:64) | 拒绝缺失连接字段或将布尔值写成字符串。 |
| UT-211 | [BothSides_RejectDuplicateUnknownAndMalformedConfiguration](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:72) | 拒绝重复字段、未知字段和错误的根 JSON 类型。 |
| UT-212 | [ParsingFailure_DoesNotEchoServerSecrets](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:82) | 解析失败不将秘密内容放入异常或内部异常。 |
| UT-213 | [Client_HttpsPageRejectsPlainWebsocketWithoutDowngrading](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:90) | HTTPS 页面拒绝 WS 配置；HTTP 本地测试及 HTTPS/WSS 正常。 |
| UT-214 | [Server_PlainModeDoesNotRequireOrLoadCertificate](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:100) | 关闭 SSL 时不要求证书路径与密码。 |
| UT-215 | [Server_SslRequiresAbsoluteCertificatePathAndExistingFile](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:108) | SSL 开启时拒绝空路径、相对路径和不存在的证书文件。 |
| UT-216 | [Server_RejectsInvalidConnectionLimit](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:118) | 总连接上限必须为正整数。 |
| UT-217 | [ServerJson_IsAlsoTheMirrorCertificateConfiguration](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:124) | Mirror 直接读取同一 JSON 的 path/password，转义密码保持一致。 |
| UT-218 | [Apply_OverridesPrefabSslPortAndCapacityWithoutCouplingClientPortToListener](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/NetworkConfigurationTest.cs:147) | 实际组件应用配置覆盖旧地址、SSL、端口模式与总连接数；客户端端口独立于监听端口。 |

### 每日任务与相关导航新增用例（39 项）

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-219 | [LowBalanceThresholdIsStrict](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:47) / 0u, DailyTaskAvailability.Claimable | 余额 0 / 999 / 1,000 可领补助，1,001 拒绝；状态和实际领取结果一致。 |
| UT-220 | [LowBalanceThresholdIsStrict](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:47) / 999u, DailyTaskAvailability.Claimable | 余额 0 / 999 / 1,000 可领补助，1,001 拒绝；状态和实际领取结果一致。 |
| UT-221 | [LowBalanceThresholdIsStrict](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:47) / 1000u, DailyTaskAvailability.Claimable | 余额 0 / 999 / 1,000 可领补助，1,001 拒绝；状态和实际领取结果一致。 |
| UT-222 | [LowBalanceThresholdIsStrict](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:47) / 1001u, DailyTaskAvailability.BalanceTooHigh | 余额 0 / 999 / 1,000 可领补助，1,001 拒绝；状态和实际领取结果一致。 |
| UT-223 | [BothRewardsHaveSeparateQuotasAndPersistAcrossRestart](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:59) | 先补助再补给，两项分别限领一次；重开数据库保持已领取，余额与账本一致。 |
| UT-224 | [ClaimRechecksBalanceInsteadOfTrustingOpenedPanel](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:76) | 打开页面后的余额变化会影响实际领取；拒绝不会消耗当天额度。 |
| UT-225 | [UtcPlusEightMidnightResetsBothQuotasAndRejectsOldPackets](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:88) | UTC 16:00 即 UTC+8 零点；两项重置，昨日请求被拒绝而不消耗今日额度。 |
| UT-226 | [ResetUsesFixedTimezoneRegardlessOfClockOffset](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:111) / -10 | 同一时刻无论时钟携带 -10 / 0 / 8 / 14 小时偏移，业务日期和下一次重置时间一致。 |
| UT-227 | [ResetUsesFixedTimezoneRegardlessOfClockOffset](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:111) / 0 | 同一时刻无论时钟携带 -10 / 0 / 8 / 14 小时偏移，业务日期和下一次重置时间一致。 |
| UT-228 | [ResetUsesFixedTimezoneRegardlessOfClockOffset](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:111) / 8 | 同一时刻无论时钟携带 -10 / 0 / 8 / 14 小时偏移，业务日期和下一次重置时间一致。 |
| UT-229 | [ResetUsesFixedTimezoneRegardlessOfClockOffset](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:111) / 14 | 同一时刻无论时钟携带 -10 / 0 / 8 / 14 小时偏移，业务日期和下一次重置时间一致。 |
| UT-230 | [InvalidTaskOrPeriodCannotMintChips](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:123) / 0, 20260905, DailyTaskResult.InvalidRequest | 非法任务编号、过去或未来业务日期不增加余额或账本条目。 |
| UT-231 | [InvalidTaskOrPeriodCannotMintChips](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:123) / 255, 20260905, DailyTaskResult.InvalidRequest | 非法任务编号、过去或未来业务日期不增加余额或账本条目。 |
| UT-232 | [InvalidTaskOrPeriodCannotMintChips](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:123) / 1, 20260904, DailyTaskResult.PeriodChanged | 非法任务编号、过去或未来业务日期不增加余额或账本条目。 |
| UT-233 | [InvalidTaskOrPeriodCannotMintChips](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:123) / 2, 20260906, DailyTaskResult.PeriodChanged | 非法任务编号、过去或未来业务日期不增加余额或账本条目。 |
| UT-234 | [ActiveHandRejectsClaimsAndNeverQueuesPayment](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:132) / false | 未结算手牌期间直接拒绝两项奖励，连已领取重放也拒绝；手牌结束不会补发。 |
| UT-235 | [ActiveHandRejectsClaimsAndNeverQueuesPayment](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:132) / true | 未结算手牌期间直接拒绝两项奖励，连已领取重放也拒绝；手牌结束不会补发。 |
| UT-236 | [ConcurrentClaimsAcrossRepositoriesCreditExactlyOnce](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:149) | 两个数据库实例并发申请同一补助，只成功发放一次，账本保持一致。 |
| UT-237 | [StorageFailureRollsBackBalanceAndQuotaAndTriggersFatalGuard](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:166) / "player_accounts", "UPDATE OF chips" | 余额更新或奖励账本写入失败时整个事务回滚，致命存储回调恰好触发一次。 |
| UT-238 | [StorageFailureRollsBackBalanceAndQuotaAndTriggersFatalGuard](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:166) / "chip_ledger", "INSERT" | 余额更新或奖励账本写入失败时整个事务回滚，致命存储回调恰好触发一次。 |
| UT-239 | [OverflowDoesNotConsumeDailyQuota](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:185) | 余额溢出拒绝且不消耗额度；刚好能容纳奖励时允许领取。 |
| UT-240 | [AccountsHaveIndependentDailyQuotas](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:196) | 不同账号的每日额度独立。 |
| UT-241 | [ServerRejectsRoomMembersAndConnectionsWithoutAccountOwnership](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:209) / "waiting" | 等待房间成员、旁观成员、残留房间关联、未认证及被替换连接不能读取或领取任务。 |
| UT-242 | [ServerRejectsRoomMembersAndConnectionsWithoutAccountOwnership](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:209) / "spectating" | 等待房间成员、旁观成员、残留房间关联、未认证及被替换连接不能读取或领取任务。 |
| UT-243 | [ServerRejectsRoomMembersAndConnectionsWithoutAccountOwnership](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:209) / "last-room" | 等待房间成员、旁观成员、残留房间关联、未认证及被替换连接不能读取或领取任务。 |
| UT-244 | [ServerRejectsRoomMembersAndConnectionsWithoutAccountOwnership](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:209) / "unauthenticated" | 等待房间成员、旁观成员、残留房间关联、未认证及被替换连接不能读取或领取任务。 |
| UT-245 | [ServerRejectsRoomMembersAndConnectionsWithoutAccountOwnership](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:209) / "replaced" | 等待房间成员、旁观成员、残留房间关联、未认证及被替换连接不能读取或领取任务。 |
| UT-246 | [ServerRejectsOutOfOrderRequestAndAcceptsFreshRetryWithoutDuplicateCredit](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:252) | 旧编号及相同编号拒绝；新编号重试返回已领取而不重复发奖。 |
| UT-247 | [MirrorRoundTripPreservesTaskSnapshotAndRequestCorrelation](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/DailyTaskTest.cs:273) | 四种 Mirror 消息往返后保留编号、账号、业务日期、中文任务、奖励金额与状态。 |
| UT-248 | [DailyTasksPrefabHasCompleteEntryCardsAndNavigationReferences](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:271) | 标题入口、两张任务卡、领取与返回刷新按钮及文本引用全部有效。 |
| UT-249 | [TaskLoginReturnsToTaskPanelUnlessRoomRecoveryHasPriority](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:292) / false | 任务入口登录返回任务页；存在上一房间时仍进入既有恢复流程。 |
| UT-250 | [TaskLoginReturnsToTaskPanelUnlessRoomRecoveryHasPriority](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:292) / true | 任务入口登录返回任务页；存在上一房间时仍进入既有恢复流程。 |
| UT-251 | [CancellingTaskLoginDoesNotRedirectLaterNormalLogin](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:309) | 取消任务登录清理意图，不影响随后普通登录的目的地。 |
| UT-252 | [TasksBackAndDisconnectPreserveTitleDestination](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:318) | 任务返回与任务页断线均正确回到标题，清空无效导航层级。 |
| UT-253 | [TasksCannotOpenDuringRoomEntryOrFromInsideRoom](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:334) / "pending" | 入房请求中、当前房间内或仍关联上次房间时，客户端阻止打开任务及领取。 |
| UT-254 | [TasksCannotOpenDuringRoomEntryOrFromInsideRoom](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:334) / "active" | 入房请求中、当前房间内或仍关联上次房间时，客户端阻止打开任务及领取。 |
| UT-255 | [TasksCannotOpenDuringRoomEntryOrFromInsideRoom](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:334) / "last-room" | 入房请求中、当前房间内或仍关联上次房间时，客户端阻止打开任务及领取。 |
| UT-256 | [ReopeningLoginImmediatelyRestoresControlsAfterPreviousPendingRequest](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:346) | 复用登录面板时立即恢复按钮状态，不保留上次请求的禁用状态。 |
| UT-257 | [TaskResponsesIgnoreEarlierOpenAndClosedPanel](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UINavigationTest.cs:358) | 忽略上一轮打开的查询、旧领取响应与关页后到达的消息，保持当前余额和页面。 |

### 结果查看期新增用例（12 项）

本轮针对性 EditMode **17/17 通过**（新增 10 项及既有相关 7 项），作业 `fc28eae951304d59a3d8ed8d34b2912d`。随后补充延迟预约边界并定向复跑 **7/7 通过**，作业 `87f31c2fe1e44dd880ffb9b8e392988d`。最后补查筹码不足座位与取消预约的名额计算，2/2 通过，作业 `36d87393695840cf8558d6c56c514dc3`。共覆盖 19 个不同用例，未运行 269 项全量套件。

| 编号 | 方法 / 参数 | 输入与预期结果 |
| --- | --- | --- |
| UT-258 | [ResultReview_PreservesBothReservationsAndSeatsUntilPreparation](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:101) | 结算事件保留离开和观战预约、座位与成员；下一手准备时才自动执行。 |
| UT-259 | [ResultReview_ImmediateChoiceOverridesExistingDeparture(false)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:124) | 已有离开预约的玩家在查看期间立即离房，剩余成员继续查看。 |
| UT-260 | [ResultReview_ImmediateChoiceOverridesExistingDeparture(true)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:124) | 已有离开预约的玩家可以立即转旁观，原预约不再自动将其移出房间。 |
| UT-261 | [ResultReview_CanCancelSitOutOrExecuteItImmediately](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:149) | 查看期间可取消观战预约，也可直接执行转旁观。 |
| UT-262 | [ResultReview_LowBalanceSeatStaysVisibleButDoesNotBlockReplacement](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:162) | 筹码不足者查看期间保留座位，但不占下一手预约名额；准备时才释放座位。 |
| UT-263 | [ResultReview_PreviousHandLeaveAndParticipationCannotAffectNewHand(false)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:185) | 下一手已开始时拒绝上一手的立即离开与转旁观请求，不变成新预约。 |
| UT-264 | [ResultReview_PreviousHandLeaveAndParticipationCannotAffectNewHand(true)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:185) | 上一手的取消离开请求不能影响新一手状态。 |
| UT-265 | [ResultReview_ButtonsOfferImmediateActionsAndSeparateCancellation(false)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:225) | 保留观战预约时仍显示立即离开、转旁观及独立取消入口；下注按钮隐藏。 |
| UT-266 | [ResultReview_ButtonsOfferImmediateActionsAndSeparateCancellation(true)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:225) | 保留离开预约时仍显示立即离开、转旁观及独立取消入口；下注按钮隐藏。 |
| UT-267 | [ResultReview_CountdownExplainsInsufficientPlayersAndClearsForNewHand](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/UIInteractionTest.cs:247) | 倒计时区分继续开局与人数不足返回，开始新手时清除旧倒计时。 |
| UT-268 | [ResultReview_ReservationArrivingAfterSettlementRemainsDeferred(false)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:204) | 本手结束前发出的离开预约延迟到查看期才抵达时，仍保留预约而不立即离房。 |
| UT-269 | [ResultReview_ReservationArrivingAfterSettlementRemainsDeferred(true)](D:/GitHubRepos/Poker/Assets/Scripts/Game/Editor/TableParticipationTest.cs:204) | 本手结束前发出的观战预约延迟到查看期才抵达时，仍保留座位和预约。 |

相关生命周期说明及一服三端执行记录见 [结果查看期验证](D:/GitHubRepos/Poker/Docs/ResultReview.md)。
