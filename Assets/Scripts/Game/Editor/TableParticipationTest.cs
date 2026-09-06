using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Game.Tests
{
    public class TableParticipationTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private RoomManager _rooms;
        private RoomInfo _room;
        private GameManager _games;
        private TableParticipationService _participation;
        private RoomDepartureService _departures;
        private ServerGameInfo Info => _games.GetGameInfo(_room.Id);

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerParticipation-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            _repository.Initialize();
            foreach (var name in new[] { "A", "B", "C", "D", "Poor" })
            {
                Assert.AreEqual(CreateAccountResult.Created, _repository.Create(new PlayerAccount
                {
                    AccountId = name, UserName = name, Chips = name == "Poor" ? 0U : 10000U,
                    PasswordAlgorithm = "Test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 2 },
                    PasswordIterations = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
                }));
            }
            _rooms = new RoomManager();
            _games = new GameManager(_repository);
            _participation = new TableParticipationService(_rooms, _games,
                name => _repository.FindByUserName(name)?.Chips, _ => true);
            _departures = new RoomDepartureService(_rooms, _games, name => _repository.SetLastRoom(name, null));
            _room = _rooms.CreateRoom("A");
            _room.MaxPlayerCount = 3;
            _room.ThinkingTime = 1000;
            Assert.AreEqual(0, _rooms.Join(_room.Id, "B"));
            Assert.AreEqual(-2, _rooms.StartGame(_room.Id));
            Assert.AreEqual(0, _rooms.PlayerChangeReady(_room.Id, "B"));
            Assert.AreEqual(0, _rooms.StartGame(_room.Id));
            Assert.AreEqual(0, _games.CreateGame(_room));
            Info.Dealer = 1;
            Assert.AreEqual(0, _games.StartNew(_room.Id));
        }

        [TearDown]
        public void TearDown()
        {
            if (_room != null)
                _games?.CancelGame(_room.Id, "ParticipationTestComplete");
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private TableParticipationResult Request(string name, TableParticipationAction action) =>
            _participation.Request(_room.Id, name, action, Info.HandId, _participation.GetRevision(name),
                !_games.RequiresDeferredLeave(_room.Id, name));

        private void JoinObserver(string name)
        {
            Assert.AreEqual(0, _rooms.Join(_room.Id, name));
            Assert.IsTrue(_repository.SetLastRoom(name, _room.Id));
        }

        private void EndHand()
        {
            while (Info.OperatingPlayer >= 0)
                Assert.AreEqual(0, _games.Fold(_room.Id, Info.OperatingPlayer));
            Assert.AreEqual(0, _games.NextStage(_room.Id));
            Assert.AreEqual(GameStage.RoundOver, Info.Stage);
            _games.PrepareNextHandSeats(_room.Id);
            _rooms.ReleaseWatchingSeats(_room.Id);
            Assert.AreEqual(0, _games.SynchronizeRoomSeats(_room.Id));
        }

        private void NextHand()
        {
            _participation.AdmitForNextHand(_room.Id);
            Assert.AreEqual(0, _games.StartNew(_room.Id));
        }

        private void SettleWithoutPreparing()
        {
            while (Info.OperatingPlayer >= 0)
                Assert.AreEqual(0, _games.Fold(_room.Id, Info.OperatingPlayer));
            Assert.AreEqual(0, _games.NextStage(_room.Id));
            Assert.AreEqual(GameStage.RoundOver, Info.Stage);
        }

        [Test]
        public void ResultReview_PreservesBothReservationsAndSeatsUntilPreparation()
        {
            Assert.AreEqual(RoomLeaveStatus.Queued, _departures.Request(_room.Id, "A"));
            Assert.AreEqual(TableParticipationResult.Success, Request("B", TableParticipationAction.SitOutAfterHand));
            SettleWithoutPreparing();
            Assert.IsNotNull(_room.FindSeat("A"));
            Assert.IsNotNull(_room.FindSeat("B"));
            Assert.IsTrue(_room.FindSeat("A").LeaveAfterHand);
            Assert.IsTrue(_room.FindSeat("B").SitOutAfterHand);
            Assert.IsEmpty(_room.Spectators);
            Assert.IsFalse(_room.IsWaiting);
            Assert.AreEqual(0, _repository.ReconcileChips().ActiveHandCount);

            _departures.CompletePending(_room.Id);
            _games.PrepareNextHandSeats(_room.Id);
            _rooms.ReleaseWatchingSeats(_room.Id);
            Assert.IsFalse(_room.ContainsMember("A"));
            Assert.IsNull(_room.FindSeat("B"));
            CollectionAssert.Contains(_room.Spectators, "B");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResultReview_ImmediateChoiceOverridesExistingDeparture(bool spectate)
        {
            Assert.AreEqual(RoomLeaveStatus.Queued, _departures.Request(_room.Id, "A"));
            SettleWithoutPreparing();
            var handId = Info.HandId;
            if (spectate)
            {
                Assert.AreEqual(TableParticipationResult.Success, Request("A", TableParticipationAction.SitOutAfterHand));
                CollectionAssert.Contains(_room.Spectators, "A");
                Assert.IsEmpty(_departures.CompletePending(_room.Id));
            }
            else
            {
                Assert.AreEqual(RoomLeaveStatus.Left,
                    _departures.RequestForHand(_room.Id, "A", handId, true));
                Assert.IsFalse(_room.ContainsMember("A"));
            }
            Assert.IsNull(_room.FindSeat("A"));
            Assert.AreEqual(handId, Info.HandId);
            Assert.IsFalse(_room.IsWaiting);
            Assert.AreEqual(1, _participation.CountNextHandPlayers(_room.Id));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void ResultReview_CanCancelSitOutOrExecuteItImmediately()
        {
            Assert.AreEqual(TableParticipationResult.Success, Request("A", TableParticipationAction.SitOutAfterHand));
            SettleWithoutPreparing();
            Assert.AreEqual(TableParticipationResult.Success, Request("A", TableParticipationAction.CancelSitOut));
            Assert.AreEqual(2, _participation.CountNextHandPlayers(_room.Id));
            Assert.IsNotNull(_room.FindSeat("A"));
            Assert.AreEqual(TableParticipationResult.Success, Request("A", TableParticipationAction.SitOutAfterHand));
            Assert.IsNull(_room.FindSeat("A"));
            CollectionAssert.Contains(_room.Spectators, "A");
        }

        [Test]
        public void ResultReview_LowBalanceSeatStaysVisibleButDoesNotBlockReplacement()
        {
            _room.MaxPlayerCount = 2;
            JoinObserver("C");
            SettleWithoutPreparing();
            // A balance below one big blind represents a busted player's next-hand eligibility.
            Assert.AreEqual(ChipMutationResult.Applied, _repository.ApplyChipMutation("A", "review-low-balance",
                "TestAdjustment", -(long)_repository.FindById("A").Chips, null, null, out _));
            Info.Players[0].Chips = 0;
            Assert.IsNotNull(_room.FindSeat("A"));
            Assert.AreEqual(1, _participation.CountNextHandPlayers(_room.Id));
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.JoinNextHand));
            Assert.IsNull(_room.FindSeat("C"));
            Assert.AreEqual(2, _participation.CountNextHandPlayers(_room.Id));
            Assert.AreEqual(RoomLeaveStatus.Queued, _departures.RequestForHand(_room.Id, "B", Info.HandId, false));
            Assert.AreEqual(RoomLeaveStatus.Cancelled, _departures.Request(_room.Id, "B", true),
                "The low-balance seat must not prevent cancellation when B and C fit the next hand.");
            _games.PrepareNextHandSeats(_room.Id);
            _rooms.ReleaseWatchingSeats(_room.Id);
            _participation.AdmitForNextHand(_room.Id);
            Assert.IsNull(_room.FindSeat("A"));
            Assert.IsNotNull(_room.FindSeat("C"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResultReview_PreviousHandLeaveAndParticipationCannotAffectNewHand(bool cancel)
        {
            SettleWithoutPreparing();
            var oldHand = Info.HandId;
            NextHand();
            Assert.AreEqual(RoomLeaveStatus.StaleRequest,
                _departures.RequestForHand(_room.Id, "A", oldHand, true, cancel));
            Assert.AreEqual(TableParticipationResult.StaleRequest,
                _participation.Request(_room.Id, "A", TableParticipationAction.SitOutAfterHand,
                    oldHand, _participation.GetRevision("A")));
            Assert.AreEqual(RoomLeaveStatus.StaleRequest,
                _departures.RequestForHand(_room.Id, "A", Info.HandId, true));
            Assert.IsFalse(_room.FindSeat("A").LeaveAfterHand);
            Assert.IsFalse(_room.FindSeat("A").SitOutAfterHand);
            Assert.IsTrue(Info.Players[0].ParticipatedThisHand);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResultReview_ReservationArrivingAfterSettlementRemainsDeferred(bool spectate)
        {
            var handId = Info.HandId;
            SettleWithoutPreparing();
            if (spectate)
            {
                Assert.AreEqual(TableParticipationResult.Success,
                    _participation.Request(_room.Id, "A", TableParticipationAction.SitOutAfterHand,
                        handId, _participation.GetRevision("A"), requireImmediate: false));
                Assert.IsTrue(_room.FindSeat("A").SitOutAfterHand);
            }
            else
            {
                Assert.AreEqual(RoomLeaveStatus.Queued,
                    _departures.RequestForHand(_room.Id, "A", handId, requireImmediate: false));
                Assert.IsTrue(_room.FindSeat("A").LeaveAfterHand);
            }
            Assert.IsNotNull(_room.FindSeat("A"));
            Assert.IsFalse(_room.IsWaiting);
            Assert.IsEmpty(_room.Spectators);
        }

        [Test]
        public void CreateJoinReadyStart_DealsOnlyTheTwoPlayers()
        {
            Assert.IsFalse(_room.IsWaiting);
            Assert.AreEqual(2, _room.PlayerCount);
            Assert.AreEqual(2, Info.Players.Count(player => player.ParticipatedThisHand));
            Assert.IsTrue(Info.Players.All(player => player.Hole.Count == 2));
            Assert.AreEqual(3, Info.TotalPot);
        }

        [Test]
        public void FullRoom_AllowsUnlimitedUnseatedObserversButRejectsParticipation()
        {
            _room.MaxPlayerCount = 2;
            JoinObserver("C");
            JoinObserver("D");
            JoinObserver("Poor");
            Assert.AreEqual(2, _room.PlayerCount);
            Assert.AreEqual(3, _room.Spectators.Count);
            Assert.AreEqual(2, _room.NextSeatIndex);
            Assert.AreEqual(TableParticipationResult.TableFull, Request("C", TableParticipationAction.JoinNextHand));
            Assert.AreEqual(TableParticipationResult.InsufficientChips, Request("Poor", TableParticipationAction.JoinNextHand));
            Assert.IsEmpty(_room.NextHandPlayers);
            var json = JsonSerializer.Serialize(new ClientGameInfo(Info, -1));
            Assert.AreEqual(1, json.Split(new[] { "\"Hole\"" }, StringSplitOptions.None).Length - 1);
            Assert.IsFalse(json.Contains("AccountId"));
            Assert.IsFalse(json.Contains("Deck"));
            Assert.IsEmpty(JsonSerializer.Deserialize<ClientGameInfo>(json).Hole);
        }

        [Test]
        public void Observer_ContinuesWatchingAcrossThreeHandsWithoutAutomaticEntry()
        {
            JoinObserver("C");
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.KeepSpectating));
            for (int hand = 0; hand < 3; hand++)
            {
                EndHand();
                NextHand();
                Assert.IsNull(_room.FindSeat("C"));
                Assert.AreEqual(-1, _games.FindPlayerSeat(_room.Id, "C"));
                Assert.IsEmpty(new ClientGameInfo(Info, -1).Hole);
                Assert.AreEqual(2, Info.Players.Count(player => player.ParticipatedThisHand));
            }
        }

        [Test]
        public void JoinNextHand_CanCancelAndAllocatesOnlyWhenTheNextHandBegins()
        {
            JoinObserver("C");
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.JoinNextHand));
            _participation.AdmitForNextHand(_room.Id);
            Assert.IsNull(_room.FindSeat("C"));
            Assert.AreEqual(2, _room.NextSeatIndex);
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.KeepSpectating));
            Assert.IsEmpty(_room.NextHandPlayers);
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.JoinNextHand));
            EndHand();
            Assert.IsNull(_room.FindSeat("C"));
            NextHand();
            Assert.AreEqual(2, _room.FindSeat("C").SeatIndex);
            Assert.AreEqual(2, new ClientGameInfo(Info, 2).Hole.Count);
            Assert.IsTrue(Info.Players[2].ParticipatedThisHand);
            Assert.IsEmpty(_room.Spectators);
        }

        [Test]
        public void Reservations_PreventOverbookingAndGuardCancellingSitOut()
        {
            _room.MaxPlayerCount = 2;
            JoinObserver("C");
            JoinObserver("D");
            Assert.AreEqual(TableParticipationResult.Success, Request("A", TableParticipationAction.SitOutAfterHand));
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.JoinNextHand));
            Assert.AreEqual(TableParticipationResult.TableFull, Request("D", TableParticipationAction.JoinNextHand));
            Assert.AreEqual(TableParticipationResult.TableFull, Request("A", TableParticipationAction.CancelSitOut));
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.KeepSpectating));
            Assert.AreEqual(TableParticipationResult.Success, Request("A", TableParticipationAction.CancelSitOut));
            Assert.AreEqual(2, _participation.CountNextHandPlayers(_room.Id));
        }

        [Test]
        public void CancelDeparture_CannotTakeAPlaceAlreadyReservedByAnObserver()
        {
            _room.MaxPlayerCount = 2;
            JoinObserver("C");
            Assert.AreEqual(RoomLeaveStatus.Queued, _departures.Request(_room.Id, "A"));
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.JoinNextHand));
            Assert.AreEqual(RoomLeaveStatus.TableFull, _departures.Request(_room.Id, "A", true));
            Assert.IsTrue(_room.FindSeat("A").LeaveAfterHand);
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.KeepSpectating));
            Assert.AreEqual(RoomLeaveStatus.Cancelled, _departures.Request(_room.Id, "A", true));
        }

        [Test]
        public void WaitingApplicant_LeavesImmediatelyAndReleasesReservation()
        {
            JoinObserver("C");
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.JoinNextHand));
            var handId = Info.HandId;
            var pot = Info.TotalPot;
            Assert.AreEqual(RoomLeaveStatus.Left, _departures.Request(_room.Id, "C"));
            Assert.IsNull(_repository.FindByUserName("C").LastRoomId);
            Assert.IsFalse(_room.ContainsMember("C"));
            Assert.IsEmpty(_room.NextHandPlayers);
            Assert.AreEqual(handId, Info.HandId);
            Assert.AreEqual(pot, Info.TotalPot);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SitOut_PreservesFoldedOrAllInRightsUntilSettlement(bool allIn)
        {
            var seat = _room.FindSeat("A").SeatIndex;
            var hole = Info.Players[seat].Hole.ToArray();
            Assert.AreEqual(TableParticipationResult.Success, Request("A", TableParticipationAction.SitOutAfterHand));
            CollectionAssert.AreEqual(hole, Info.Players[seat].Hole);
            Assert.IsFalse(Info.Players[seat].IsFold);
            Assert.IsTrue(_room.ContainsMember("A"));
            Assert.AreEqual(0, allIn ? _games.AllIn(_room.Id, seat) : _games.Fold(_room.Id, seat));
            Assert.IsNotNull(_room.FindSeat("A"));
            EndHand();
            Assert.IsNull(_room.FindSeat("A"));
            CollectionAssert.Contains(_room.Spectators, "A");
            Assert.AreEqual(1, _participation.CountNextHandPlayers(_room.Id));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void SitOut_DisconnectAndReconnectKeepsIntentAndCurrentHandRights()
        {
            Assert.AreEqual(TableParticipationResult.Success, Request("B", TableParticipationAction.SitOutAfterHand));
            _games.SetDisconnected(_room.Id, "B");
            Assert.AreEqual(1, _games.SetConnected(_room.Id, "B"));
            Assert.IsTrue(_room.FindSeat("B").SitOutAfterHand);
            Assert.AreEqual(2, Info.Players[1].Hole.Count);
        }

        [Test]
        public void DepartedSeat_IsNotReusedAndSpectatorReentryGetsANewNumber()
        {
            JoinObserver("C");
            Assert.AreEqual(TableParticipationResult.Success, Request("A", TableParticipationAction.SitOutAfterHand));
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.JoinNextHand));
            EndHand();
            NextHand();
            Assert.AreEqual(1, _room.FindSeat("B").SeatIndex);
            Assert.AreEqual(2, _room.FindSeat("C").SeatIndex);
            Assert.IsFalse(Info.Players[0].IsOccupied);
            Assert.AreEqual(TableParticipationResult.Success, Request("A", TableParticipationAction.JoinNextHand));
            EndHand();
            NextHand();
            Assert.AreEqual(3, _room.FindSeat("A").SeatIndex);
            Assert.AreEqual(3, _room.PlayerCount);
            Assert.AreEqual(4, _room.NextSeatIndex);
        }

        [TestCase(4, false)]
        [TestCase(5, true)]
        public void CompactSeats_OnlyAboveHalfEmptyAndOnlyBeforeANewHand(int span, bool compacts)
        {
            _room.NextSeatIndex = span;
            Assert.AreEqual(0, _games.SynchronizeRoomSeats(_room.Id));
            Assert.AreEqual(-2, _games.StartNew(_room.Id));
            Assert.AreEqual(span, Info.PlayersCount);
            EndHand();
            NextHand();
            Assert.AreEqual(compacts ? 2 : span, Info.PlayersCount);
            Assert.AreEqual(compacts ? 2 : span, _room.NextSeatIndex);
            Assert.AreEqual(0, _room.FindSeat("A").SeatIndex);
            Assert.AreEqual(1, _room.FindSeat("B").SeatIndex);
        }

        [Test]
        public void SparseSeats_BlindsRotatePastGapsAcrossMultipleHands()
        {
            JoinObserver("C");
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.JoinNextHand));
            EndHand();
            _participation.AdmitForNextHand(_room.Id);
            _room.FindSeat("B").SeatIndex = 2;
            _room.FindSeat("C").SeatIndex = 4;
            _room.SynchronizeLegacyLists();
            Assert.AreEqual(0, _games.SynchronizeRoomSeats(_room.Id));
            Info.Dealer = 4;
            var expected = new[] { 4, 0, 2, 4, 0, 2 };
            foreach (var bigBlind in expected)
            {
                NextHand();
                Assert.AreEqual(bigBlind, Info.BigBlind);
                Assert.IsTrue(Info.Players[Info.Dealer].IsOccupied);
                Assert.IsTrue(Info.Players[Info.SmallBlind].IsOccupied);
                EndHand();
            }
        }

        [TestCase(PlayerOperationType.Check)]
        [TestCase(PlayerOperationType.Call)]
        [TestCase(PlayerOperationType.Fold)]
        [TestCase(PlayerOperationType.AllIn)]
        public void OldTurnRequest_IsRejectedWhenSamePlayerActsOnTheNextStreet(PlayerOperationType type)
        {
            var old = new PlayerOperationRequest
            {
                RoomId = _room.Id, PlayerIndex = 0, HandId = Info.HandId, TurnId = Info.TurnId, Type = type,
            };
            Assert.AreEqual(0, _games.Call(_room.Id, 0));
            Info.Timer.OnTimeUp.Invoke();
            Assert.AreEqual(0, _games.NextStage(_room.Id));
            Assert.AreEqual(0, _games.Check(_room.Id, 1));
            Assert.AreEqual(0, Info.OperatingPlayer);
            var pot = Info.TotalPot;
            var turn = Info.TurnId;
            Assert.AreEqual(-8, _games.ExecuteOperation(old));
            Assert.AreEqual(pot, Info.TotalPot);
            Assert.AreEqual(turn, Info.TurnId);
            Assert.AreEqual(0, Info.OperatingPlayer);
        }

        [Test]
        public void DuplicateAndPreviousHandOperations_AreRejectedWithoutChangingBalances()
        {
            var request = new PlayerOperationRequest
            {
                RoomId = _room.Id, PlayerIndex = 0, HandId = Info.HandId, TurnId = Info.TurnId,
                Type = PlayerOperationType.Call,
            };
            Assert.AreEqual(0, _games.ExecuteOperation(request));
            var balance = _repository.FindByUserName("A").Chips;
            Assert.AreEqual(-8, _games.ExecuteOperation(request));
            Assert.AreEqual(balance, _repository.FindByUserName("A").Chips);
            EndHand();
            NextHand();
            request.TurnId = Info.TurnId;
            request.PlayerIndex = Info.OperatingPlayer;
            Assert.AreEqual(-8, _games.ExecuteOperation(request));
            var restored = JsonSerializer.Deserialize<ClientGameInfo>(JsonSerializer.Serialize(new ClientGameInfo(Info, 0)));
            Assert.AreEqual(Info.HandId, restored.HandId);
            Assert.AreEqual(Info.TurnId, restored.TurnId);
        }

        [Test]
        public void OldParticipationIntent_CannotUndoANewerChoice()
        {
            JoinObserver("C");
            var revision = _participation.GetRevision("C");
            Assert.AreEqual(TableParticipationResult.Success, Request("C", TableParticipationAction.JoinNextHand));
            Assert.AreEqual(TableParticipationResult.StaleRequest,
                _participation.Request(_room.Id, "C", TableParticipationAction.KeepSpectating, Info.HandId, revision));
            CollectionAssert.Contains(_room.NextHandPlayers, "C");
        }

        [Test]
        public void SpectatorSettlement_ContainsNoHoleCardsWithoutMutatingPlayerResult()
        {
            var result = new RoundResult();
            result.PlayerResults.Add(new RoundResult.Player { PlayerName = "A", Hole = Info.Players[0].Hole });
            var projected = result.ForSpectator();
            Assert.IsTrue(projected.PlayerResults.All(player => player.Hole.Count == 0));
            Assert.AreEqual(2, result.PlayerResults[0].Hole.Count);
        }
    }
}
