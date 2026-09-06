using Framework.SO;
using Framework.Timer;
using Game.Network;
using Game.UI;
using Game.UI.GamePanelElement;
using Game.UI.RoomListPanelElement;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class UIInteractionTest
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("UIInteractionTest", typeof(RectTransform), typeof(Canvas));
            _root.SetActive(false);
            if (!SingletonSOManager.Available)
            {
                var manager = new GameObject("CardResources").AddComponent<SingletonSOManager>();
                manager.transform.SetParent(_root.transform);
                Invoke(manager, "Awake");
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _root.GetComponentsInChildren<PlayerListItem>(true))
                item.IsOperating = false;
            Object.DestroyImmediate(_root);
        }

        [TestCase("1", 200u)]
        [TestCase("9000", 1000u)]
        [TestCase("350", 350u)]
        [TestCase("invalid", 200u)]
        [TestCase("18446744073709551615", 1000u)]
        public void BetInput_CorrectionUpdatesBothDisplayAndSubmittedTotal(string input, uint expected)
        {
            var area = CreateGamePanel().GetComponentInChildren<OperationArea>(true);
            area.Info = CreateInfo();
            var field = Field<TMP_InputField>(area, "_betInput");
            field.text = input;
            Invoke(area, "OnBetOrRaiseChipsChanged", input);

            Assert.AreEqual(expected.ToString(), field.text);
            Assert.AreEqual(expected, Field<uint>(area, "_betChips"));
        }

        [Test]
        public void BetInput_ShortAllInClampsToAvailableTotalIncludingPriorBet()
        {
            var area = CreateGamePanel().GetComponentInChildren<OperationArea>(true);
            var info = CreateInfo();
            var player = info.Players[0];
            player.StageBet = 50;
            player.Chips = 100;
            info.Players[0] = player;
            area.Info = info;
            Invoke(area, "OnBetOrRaiseChipsChanged", "1");

            Assert.AreEqual(150u, Field<uint>(area, "_betChips"));
            Assert.IsTrue(Field<Button>(area, "_betOrRaiseButton").gameObject.activeSelf);
        }

        [Test]
        public void BetInput_UnrelatedSyncPreservesEditingAndTurnChangeResetsIt()
        {
            var area = CreateGamePanel().GetComponentInChildren<OperationArea>(true);
            var info = CreateInfo();
            area.Info = info;
            var field = Field<TMP_InputField>(area, "_betInput");
            field.text = "375";
            area.Info = info;
            Assert.AreEqual("375", field.text);

            info.CurrentHighestPlayerBet = 300;
            area.Info = info;
            Assert.AreEqual("400", field.text);
        }

        [Test]
        public void Actions_SpectatorsAndClosedRaiseRightsCannotUseBetInput()
        {
            var area = CreateGamePanel().GetComponentInChildren<OperationArea>(true);
            var info = CreateInfo();
            info.CanRaise = false;
            area.Info = info;
            Assert.IsFalse(Field<TMP_InputField>(area, "_betInput").gameObject.activeSelf);
            Assert.IsFalse(Field<Button>(area, "_allInButton").gameObject.activeSelf);
            Assert.IsTrue(Field<Button>(area, "_callButton").gameObject.activeSelf);

            info.CurrentPlayerIndex = -1;
            area.Info = info;
            Assert.IsFalse(area.GetComponentsInChildren<Button>(true).Any(button => button.gameObject.activeSelf));
        }

        [Test]
        public void GamePanel_ResizesAndHidesVacantSeatsAcrossRooms()
        {
            var panel = CreateGamePanel();
            panel.Info = CreateInfo(2);
            var info = CreateInfo(8);
            info.Players[6] = default;
            panel.Info = info;
            var rows = Field<List<PlayerListItem>>(panel, "_playerListItems");
            Assert.AreEqual(8, rows.Count);
            Assert.AreEqual(7, rows.Count(row => row.gameObject.activeSelf));
            Assert.IsFalse(Field<TextMeshProUGUI>(rows[1], "_timeText").gameObject.activeSelf);

            panel.Info = CreateInfo(2);
            Assert.AreEqual(2, rows.Count(row => row.gameObject.activeSelf));
        }

        [Test]
        public void PlayerCountdown_DuplicateSyncDoesNotRestartElapsedTime()
        {
            var panel = CreateGamePanel();
            var info = CreateInfo();
            panel.Info = info;
            var row = Field<List<PlayerListItem>>(panel, "_playerListItems")[0];
            var timer = Field<Timer>(row, "_timer");
            typeof(Timer).GetField("_time", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(timer, 12d);
            panel.Info = info;
            Assert.AreEqual(18d, timer.TimeRemaining);

            info.Round = GameStage.Flop;
            panel.Info = info;
            Assert.AreEqual(30d, timer.TimeRemaining);
        }

        [Test]
        public void RoundResult_HiddenOrFoldedHandsNeverDisplayAnInferredPattern()
        {
            var panel = Create<RoundResultMsgBox>("GamePanel/RoundResultMsgBox.prefab");
            panel.Info = new RoundResult
            {
                Community = Poker.GetAll().Where(card => card.Suit == PokerSuit.Heart).Take(5).ToList(),
                PlayerResults = new List<RoundResult.Player>
                {
                    new RoundResult.Player { PlayerName = "Folded", IsFold = true },
                    new RoundResult.Player { PlayerName = "Winner", ChipEarned = 200,
                        Hole = new List<Poker> { new Poker(PokerSuit.Spade, PokerRank.Ace), new Poker(PokerSuit.Club, PokerRank.Ace) } },
                    new RoundResult.Player { PlayerName = "Hidden" },
                },
            };
            var rows = Field<List<RoundResultListItem>>(panel, "_listItems");
            Assert.IsTrue(rows.Single(row => row.PlayerName == "Winner").ShowPattern);
            Assert.IsFalse(rows.Single(row => row.PlayerName == "Folded").ShowPattern);
            Assert.IsFalse(rows.Single(row => row.PlayerName == "Hidden").ShowPattern);
        }

        [Test]
        public void ResolutionDropdown_FirstOpenSelectsActualSizeInDeduplicatedList()
        {
            var panel = Create<OptionPanel>("OptionPanel.prefab");
            Invoke(panel, "RefreshResolutions");
            var dropdown = Field<TMP_Dropdown>(panel, "_resolution");
            Assert.AreEqual($"{Screen.width}x{Screen.height}", dropdown.options[dropdown.value].text);
            Assert.AreEqual(dropdown.options.Count, dropdown.options.Select(option => option.text).Distinct().Count());
        }

        [Test]
        public void GameResults_SortsUnsignedChipTotalsWithoutOverflow()
        {
            var panel = Create<GameResultMsgBox>("GamePanel/GameResultMsgBox.prefab");
            var result = new GameResult
            {
                PlayerResults = new List<GameResult.Player>
                {
                    new GameResult.Player { Name = "Low", Chips = 1 },
                    new GameResult.Player { Name = "High", Chips = uint.MaxValue },
                },
            };
            panel.Info = result;
            Assert.AreEqual("High", result.PlayerResults[0].Name);
        }

        [Test]
        public void RoomList_PeriodicRefreshPreservesExistingJoinButtons()
        {
            var panel = Create<RoomListPanel>("RoomListPanel/RoomListPanel.prefab");
            panel.Rooms = new RoomList { Rooms = new List<RoomInfo> { new RoomInfo { Id = 7, OwnerName = "Alice" } } };
            var items = Field<Dictionary<int, Game.UI.RoomListPanelElement.RoomListItem>>(panel, "_roomItems");
            var original = items[7];
            panel.Rooms = new RoomList { Rooms = new List<RoomInfo> { new RoomInfo { Id = 7, OwnerName = "Bob" } } };
            Assert.AreSame(original, items[7]);
            Assert.AreEqual(1, items.Count);
        }

        [Test]
        public void DepartureControls_UseServerStateAndKeepBettingAvailableWhileQueued()
        {
            var panel = CreateGamePanel();
            var sync = new GameInfoSync { Info = CreateInfo(), LeaveAfterHand = true };
            panel.ApplySync(sync);
            Assert.AreEqual("取消离开", Field<TextMeshProUGUI>(panel, "_leaveButtonLabel").text);
            Assert.IsTrue(Field<TextMeshProUGUI>(panel, "_leaveStatus").gameObject.activeSelf);
            Assert.IsTrue(Field<Button>(Field<OperationArea>(panel, "_operationArea"), "_callButton").gameObject.activeSelf);

            panel.SetLeaveRequestInFlight(true);
            Assert.IsFalse(Field<Button>(panel, "_leaveButton").interactable);
            panel.SetLeaveRequestInFlight(false);
            sync.LeaveAfterHand = false;
            sync.CanLeaveImmediately = true;
            panel.ApplySync(sync);
            Assert.AreEqual("离开房间", Field<TextMeshProUGUI>(panel, "_leaveButtonLabel").text);
            Assert.IsFalse(Field<TextMeshProUGUI>(panel, "_leaveStatus").gameObject.activeSelf);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResultReview_ButtonsOfferImmediateActionsAndSeparateCancellation(bool leave)
        {
            var panel = CreateGamePanel();
            var info = CreateInfo();
            info.Round = GameStage.RoundOver;
            info.OperatingPlayer = -1;
            panel.ApplySync(new GameInfoSync
            {
                Info = info, LeaveAfterHand = leave, SitOutAfterHand = !leave,
                CanLeaveImmediately = true,
            });
            Assert.AreEqual("离开房间", Field<TextMeshProUGUI>(panel, "_leaveButtonLabel").text);
            Assert.AreEqual("转为旁观", Field<TextMeshProUGUI>(panel, "_participationButtonLabel").text);
            Assert.IsTrue(Field<Button>(panel, "_participationButton").interactable);
            var cancel = Field<Button>(panel, "_keepSpectatingButton");
            Assert.IsTrue(cancel.gameObject.activeSelf);
            Assert.AreEqual(leave ? "取消离开预约" : "取消观战预约", cancel.GetComponentInChildren<TMP_Text>().text);
            Assert.IsFalse(Field<OperationArea>(panel, "_operationArea").GetComponentsInChildren<Button>(true)
                .Any(button => button.gameObject.activeSelf));
        }

        [Test]
        public void ResultReview_CountdownExplainsInsufficientPlayersAndClearsForNewHand()
        {
            var panel = CreateGamePanel();
            panel.Info = CreateInfo();
            panel.SetInterHandCountdown(true, 7, false);
            Assert.AreEqual("当前可参战人数不足，7 秒后返回", Field<TextMeshProUGUI>(panel, "_countdownStatus").text);
            panel.SetInterHandCountdown(true, 6, true);
            Assert.AreEqual("下一手将在 6 秒后开始", Field<TextMeshProUGUI>(panel, "_countdownStatus").text);
            panel.ApplySync(new GameInfoSync { Info = CreateInfo() });
            Assert.IsFalse(Field<TextMeshProUGUI>(panel, "_countdownStatus").gameObject.activeSelf);
        }

        [Test]
        public void HidingGamePanel_StopsSeatTimersAndClearsInterHandCountdown()
        {
            var panel = CreateGamePanel();
            panel.Info = CreateInfo();
            panel.SetInterHandCountdown(true, 7);
            Invoke(panel, "OnDisable");
            Assert.IsTrue(Field<List<PlayerListItem>>(panel, "_playerListItems").All(player => !player.IsOperating));
            Assert.IsFalse(Field<TextMeshProUGUI>(panel, "_countdownStatus").gameObject.activeSelf);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SpectatorControls_ClearPrivateCardsAndReflectAvailableSeats(bool hasPlace)
        {
            var panel = CreateGamePanel();
            var info = CreateInfo();
            info.Hole.Add(new Poker(PokerSuit.Spade, PokerRank.Ace));
            info.Hole.Add(new Poker(PokerSuit.Heart, PokerRank.Ace));
            panel.ApplySync(new GameInfoSync { Info = info, ParticipationStatus = TableParticipationStatus.Playing });
            var area = Field<OperationArea>(panel, "_operationArea");
            Assert.IsTrue(Field<List<PokerElement>>(area, "_hole").All(card => card.IsShowing));

            info.CurrentPlayerIndex = -1;
            info.Hole.Clear();
            panel.ApplySync(new GameInfoSync
            {
                Info = info, ParticipationStatus = TableParticipationStatus.Spectating,
                CanLeaveImmediately = true, CanJoinNextHand = hasPlace, AccountChips = 1000, BigBlind = 100,
            });
            Assert.IsTrue(Field<List<PokerElement>>(area, "_hole").All(card => !card.IsShowing));
            Assert.IsFalse(area.GetComponentsInChildren<Button>(true).Any(button => button.gameObject.activeSelf));
            Assert.AreEqual("旁观中", Field<TextMeshProUGUI>(panel, "_tableStatus").text);
            Assert.AreEqual("离开房间", Field<TextMeshProUGUI>(panel, "_leaveButtonLabel").text);
            Assert.AreEqual(hasPlace, Field<Button>(panel, "_participationButton").interactable);
            Assert.AreEqual(hasPlace ? "下一手参战" : "参战名额已满",
                Field<TextMeshProUGUI>(panel, "_participationButtonLabel").text);
            Assert.IsTrue(Field<Button>(panel, "_keepSpectatingButton").gameObject.activeSelf);
        }

        [Test]
        public void PendingParticipation_UIAllowsContinuingToWatchAndClearsOnDisable()
        {
            var panel = CreateGamePanel();
            var info = CreateInfo();
            info.CurrentPlayerIndex = -1;
            panel.ApplySync(new GameInfoSync
            {
                Info = info, JoinNextHand = true, CanJoinNextHand = true,
                ParticipationStatus = TableParticipationStatus.WaitingForNextHand, CanLeaveImmediately = true,
            });
            Assert.AreEqual("已申请下一手", Field<TextMeshProUGUI>(panel, "_participationButtonLabel").text);
            Assert.IsFalse(Field<Button>(panel, "_participationButton").interactable);
            Assert.IsTrue(Field<Button>(panel, "_keepSpectatingButton").interactable);
            panel.SetParticipationRequestInFlight(true);
            Assert.IsFalse(Field<Button>(panel, "_keepSpectatingButton").interactable);
            Invoke(panel, "OnDisable");
            Assert.IsFalse(Field<bool>(panel, "_participationRequestInFlight"));
        }

        [Test]
        public void SitOutControls_KeepCurrentBettingRightsAndDisplayCancellation()
        {
            var panel = CreateGamePanel();
            panel.ApplySync(new GameInfoSync
            {
                Info = CreateInfo(), SitOutAfterHand = true, ParticipationStatus = TableParticipationStatus.Playing,
            });
            Assert.AreEqual("取消转旁观", Field<TextMeshProUGUI>(panel, "_participationButtonLabel").text);
            Assert.IsTrue(Field<TextMeshProUGUI>(panel, "_tableStatus").text.Contains("查看结果结束后"));
            Assert.IsTrue(Field<Button>(Field<OperationArea>(panel, "_operationArea"), "_callButton").gameObject.activeSelf);
            Assert.IsFalse(Field<Button>(panel, "_keepSpectatingButton").gameObject.activeSelf);
        }

        [Test]
        public void FullActiveRoom_StillHasAnEnabledLobbyEntryForSpectators()
        {
            var panel = Create<RoomListPanel>("RoomListPanel/RoomListPanel.prefab");
            var room = RoomInfo.CreateRoom("Owner");
            room.MaxPlayerCount = 2;
            room.IsWaiting = false;
            room.Seats.Add(new RoomSeatInfo { SeatIndex = 1, PlayerName = "Bob", State = RoomSeatState.Playing });
            room.Spectators.Add("Carol");
            room.SynchronizeLegacyLists();
            panel.Rooms = new RoomList { Rooms = new List<RoomInfo> { room } };
            var items = Field<Dictionary<int, RoomListItem>>(panel, "_roomItems");
            Assert.IsTrue(items[room.Id].GetComponent<Button>().interactable);
            Assert.IsTrue(Field<TextMeshProUGUI>(items[room.Id], "_capacity").text.Contains("旁观 1"));
        }

        [Test]
        public void ServerShutdownNotice_ShowsStorageReasonWithoutRecoveryPromises()
        {
            var box = Create<MsgBox>("MsgBox.prefab");
            NetworkRoom.ConfigureServerShutdownMessage(box);
            Assert.AreEqual("服务器已停止服务", box.Title);
            StringAssert.Contains("数据保存失败", box.Message);
            Assert.AreEqual(MsgboxType.Confirm, box.Type);
            Assert.AreEqual("知道了", box.ConfirmButtonText);
            Assert.IsFalse(box.ShowCloseButton);
            StringAssert.DoesNotContain("已经保存", box.Message);
            StringAssert.DoesNotContain("检查网络", box.Message);
        }

        [TestCase(PlayerOperationType.AllIn, 0u, 100u, 1000u, 200u, true)]
        [TestCase(PlayerOperationType.Call, 0u, 100u, 100u, 200u, true)]
        [TestCase(PlayerOperationType.Call, 0u, 100u, 50u, 200u, true)]
        [TestCase(PlayerOperationType.Call, 0u, 100u, 101u, 200u, false)]
        [TestCase(PlayerOperationType.Bet, 1100u, 100u, 1000u, 200u, true)]
        [TestCase(PlayerOperationType.Bet, 1099u, 100u, 1000u, 200u, false)]
        [TestCase(PlayerOperationType.Fold, 0u, 100u, 1000u, 200u, false)]
        public void AllInConfirmation_IncludesPriorStageBetAndShortCalls(PlayerOperationType type,
            uint number, uint stageBet, uint chips, uint highest, bool expected)
        {
            var area = CreateGamePanel().GetComponentInChildren<OperationArea>(true);
            var info = CreateInfo();
            var player = info.Players[0];
            player.StageBet = stageBet;
            player.Chips = chips;
            info.Players[0] = player;
            info.CurrentHighestPlayerBet = highest;
            area.Info = info;
            var actual = (bool)typeof(OperationArea).GetMethod("UsesAllRemainingChips",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(area, new object[] { type, number });
            Assert.AreEqual(expected, actual);
        }

        [TestCase("1", "最低")]
        [TestCase("9000", "全下")]
        [TestCase("invalid", "有效")]
        public void BetInput_CorrectionExplainsReasonAndNewTurnClearsIt(string input, string expected)
        {
            var area = CreateGamePanel().GetComponentInChildren<OperationArea>(true);
            var info = CreateInfo();
            area.Info = info;
            Invoke(area, "OnBetOrRaiseChipsChanged", input);
            var hint = Field<TextMeshProUGUI>(area, "_betHint");
            Assert.IsNotNull(hint);
            StringAssert.Contains(expected, hint.text);
            area.Info = info;
            StringAssert.Contains(expected, hint.text);
            info.TurnId++;
            area.Info = info;
            Assert.IsFalse(hint.gameObject.activeSelf);
        }

        [TestCase("ChangeSmallBlindChip", "0", "小盲")]
        [TestCase("ChangeTime", "NaN", "思考时间")]
        [TestCase("ChangeMaxPlayer", "1", "参战人数")]
        public void RoomInput_InvalidValueExplainsCorrection(string method, string input, string expected)
        {
            var panel = Create<RoomPanel>("RoomPanel/RoomPanel.prefab");
            Invoke(panel, method, input);
            var hint = Field<TextMeshProUGUI>(panel, "_settingsHint");
            Assert.IsNotNull(hint);
            StringAssert.Contains(expected, hint.text);
            StringAssert.Contains("已恢复原值", hint.text);
        }

        [TestCase(false, "房间等待")]
        [TestCase(true, "房间列表")]
        public void InsufficientPlayers_ExplainsCorrectDestination(bool spectator, string destination)
        {
            var box = Create<MsgBox>("MsgBox.prefab");
            NetworkRoom.ConfigureRoomTransition(box, RoomTransitionReason.InsufficientPlayers, spectator);
            StringAssert.Contains("不足两人", box.Message);
            StringAssert.Contains(destination, box.Message);
            Assert.AreEqual("知道了", box.ConfirmButtonText);
        }

        [Test]
        public void SpectatorIntro_ExplainsExplicitEntryAndSeatLimit()
        {
            var box = Create<MsgBox>("MsgBox.prefab");
            NetworkRoom.ConfigureSpectatorIntro(box);
            StringAssert.Contains("下一手参战", box.Message);
            StringAssert.Contains("名额只统计参战玩家", box.Message);
            StringAssert.Contains("当前不会为你发牌", box.Message);
        }

        [Test]
        public void RiskConfirmation_DefaultFocusIsCancel()
        {
            var previous = UnityEngine.EventSystems.EventSystem.current;
            var go = new GameObject("Confirmation focus", typeof(UnityEngine.EventSystems.EventSystem));
            try
            {
                var eventSystem = go.GetComponent<UnityEngine.EventSystems.EventSystem>();
                Invoke(eventSystem, "OnEnable");
                UnityEngine.EventSystems.EventSystem.current = eventSystem;
                var box = Create<MsgBox>("MsgBox.prefab");
                box.Type = MsgboxType.ConfirmAndCancel;
                box.PreferCancel = true;
                Invoke(box, "SelectDefaultButton");
                Assert.AreSame(Field<Button>(box, "_cancelButton").gameObject,
                    UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject);
            }
            finally
            {
                Invoke(go.GetComponent<UnityEngine.EventSystems.EventSystem>(), "OnDisable");
                Object.DestroyImmediate(go);
                if (previous != null)
                    UnityEngine.EventSystems.EventSystem.current = previous;
            }
        }

        [Test]
        public void TransitionMessages_RoundTripExplicitReasonThroughMirror()
        {
            var writer = new Mirror.NetworkWriter();
            writer.Write(new RoomInfoSync { RoomInfo = new RoomInfo { Id = 9 },
                TransitionReason = RoomTransitionReason.InsufficientPlayers });
            writer.Write(new PlayerLeaveRoomResponse { RoomId = 9, Status = RoomLeaveStatus.Left,
                TransitionReason = RoomTransitionReason.InsufficientPlayers });
            var reader = new Mirror.NetworkReader(writer.ToArraySegment());
            var room = reader.Read<RoomInfoSync>();
            var leave = reader.Read<PlayerLeaveRoomResponse>();
            Assert.AreEqual(9, room.RoomInfo.Id);
            Assert.AreEqual(RoomTransitionReason.InsufficientPlayers, room.TransitionReason);
            Assert.AreEqual(RoomTransitionReason.InsufficientPlayers, leave.TransitionReason);
            Assert.AreEqual(RoomLeaveStatus.Left, leave.Status);
        }

        private GamePanel CreateGamePanel()
        {
            var panel = Create<GamePanel>("GamePanel/GamePanel.prefab");
            Create<InGameOptionPanel>("InGameOptionPanel.prefab").Bind(panel);
            return panel;
        }

        private T Create<T>(string path) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/" + path);
            return Object.Instantiate(prefab, _root.transform).GetComponent<T>();
        }

        private static ClientGameInfo CreateInfo(int count = 2)
        {
            return new ClientGameInfo
            {
                RoomInfo = new RoomInfo { Id = 42, ThinkingTime = 30 },
                Players = Enumerable.Range(0, count).Select(i => new ClientGameInfo.PlayerInfo
                {
                    SeatIndex = i, IsOccupied = true, PlayerName = "Player" + i, Chips = 1000,
                }).ToList(),
                Hole = new List<Poker>(),
                Community = new List<Poker>(),
                CurrentPlayerIndex = 0,
                OperatingPlayer = 0,
                CurrentHighestPlayerBet = 100,
                CurrentHighestRaise = 100,
                CanRaise = true,
            };
        }

        private static T Field<T>(object target, string name)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    return (T)field.GetValue(target);
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static void Invoke(object target, string name, params object[] args)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        }
    }
}
