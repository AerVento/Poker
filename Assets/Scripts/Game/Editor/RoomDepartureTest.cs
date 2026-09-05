using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Game.Tests
{
    public class RoomDepartureTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private RoomManager _rooms;
        private GameManager _games;
        private RoomDepartureService _departures;
        private RoomInfo _room;
        private bool _storageAvailable;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerDeparture-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            _repository.Initialize();
            _rooms = new RoomManager();
            _games = new GameManager(_repository);
            _storageAvailable = true;
            _departures = new RoomDepartureService(_rooms, _games, name =>
                _storageAvailable && _repository.SetLastRoom(_repository.FindByUserName(name).AccountId, null));
            foreach (var name in new[] { "Owner", "Bob", "Carol", "Guest" })
            {
                Assert.AreEqual(CreateAccountResult.Created, _repository.Create(new PlayerAccount
                {
                    AccountId = name,
                    UserName = name,
                    PasswordAlgorithm = "Test",
                    PasswordHash = new byte[] { 1 },
                    PasswordSalt = new byte[] { 2 },
                    PasswordIterations = 1,
                    Chips = 1000,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                }));
            }
            _room = _rooms.CreateRoom("Owner");
            _room.ThinkingTime = 1000;
            _room.MaxPlayerCount = 4;
            foreach (var name in new[] { "Bob", "Carol" })
            {
                _rooms.Join(_room.Id, name);
                _rooms.PlayerChangeReady(_room.Id, name);
            }
            foreach (var name in _room.Players)
                _repository.SetLastRoom(name, _room.Id);
            _rooms.StartGame(_room.Id);
            Assert.AreEqual(0, _games.CreateGame(_room));
            _games.GetGameInfo(_room.Id).Dealer = 2;
            Assert.AreEqual(0, _games.StartNew(_room.Id));
        }

        [TearDown]
        public void TearDown()
        {
            _games.CancelGame(_room.Id, "TestComplete");
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                if (File.Exists(_path + suffix))
                    File.Delete(_path + suffix);
            }
        }

        [Test]
        public void QueueAndCancel_PreserveTurnCardsChipsMembershipAndActionRights()
        {
            var info = _games.GetGameInfo(_room.Id);
            var player = info.Players[0];
            var chips = player.Chips;
            var hole = player.Hole.ToArray();
            int stageEvents = 0;
            _games.OnGameStageEnded += _ => stageEvents++;

            Assert.AreEqual(RoomLeaveStatus.Queued, _departures.Request(_room.Id, "Owner"));
            Assert.AreEqual(RoomLeaveStatus.Queued, _departures.Request(_room.Id, "Owner"));
            Assert.AreEqual(0, info.OperatingPlayer);
            Assert.IsFalse(player.IsFold);
            Assert.IsFalse(player.IsLeaving);
            Assert.AreEqual(chips, player.Chips);
            CollectionAssert.AreEqual(hole, player.Hole);
            Assert.AreEqual("Owner", _room.OwnerName);
            Assert.IsTrue(_room.ContainsMember("Owner"));
            Assert.AreEqual(_room.Id, _repository.FindByUserName("Owner").LastRoomId);
            Assert.AreEqual(0, _games.Call(_room.Id, 0));
            Assert.AreEqual(0, stageEvents);
            Assert.AreEqual(RoomLeaveStatus.Cancelled, _departures.Request(_room.Id, "Owner", true));
            Assert.IsFalse(_room.FindSeat("Owner").LeaveAfterHand);
            Assert.AreEqual(3, _games.CountEligiblePlayers(_room.Id));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FoldedAndAllInParticipants_StillWaitForSettlement(bool allIn)
        {
            var player = _games.GetGameInfo(_room.Id).Players[0];
            if (allIn)
                Assert.AreEqual(0, _games.AllIn(_room.Id, 0));
            else
                Assert.AreEqual(0, _games.Fold(_room.Id, 0));
            Assert.AreEqual(RoomLeaveStatus.Queued, _departures.Request(_room.Id, "Owner"));
            Assert.IsTrue(_games.RequiresDeferredLeave(_room.Id, "Owner"));
            Assert.IsEmpty(_departures.CompletePending(_room.Id));
            Assert.IsTrue(_room.ContainsMember("Owner"));
            Assert.AreEqual(allIn, player.IsAllIn);
        }

        [Test]
        public void WaitingJoinerAndUnseatedSpectator_LeaveImmediatelyDuringActiveHand()
        {
            _rooms.Join(_room.Id, "Guest");
            _repository.SetLastRoom("Guest", _room.Id);
            Assert.AreEqual(0, _games.SynchronizeRoomSeats(_room.Id));
            Assert.IsFalse(_games.RequiresDeferredLeave(_room.Id, "Guest"));
            Assert.AreEqual(RoomLeaveStatus.Left, _departures.Request(_room.Id, "Guest"));
            _games.SynchronizeRoomSeats(_room.Id);
            Assert.IsFalse(_room.ContainsMember("Guest"));
            Assert.IsTrue(_games.IsHandInProgress(_room.Id));

            Assert.AreEqual(1, _rooms.Restore(_room.Id, "Guest", false));
            Assert.IsTrue(_room.Spectators.Contains("Guest"));
            Assert.AreEqual(RoomLeaveStatus.Left, _departures.Request(_room.Id, "Guest"));
            Assert.IsFalse(_room.ContainsMember("Guest"));
            Assert.IsNull(_repository.FindByUserName("Guest").LastRoomId);
        }

        [Test]
        public void Settlement_CreditsAllInLeaverBeforeRemovingAndTransferringOwner()
        {
            var info = _games.GetGameInfo(_room.Id);
            Assert.AreEqual(0, _games.Bet(_room.Id, 0, 1000));
            Assert.AreEqual(RoomLeaveStatus.Queued, _departures.Request(_room.Id, "Owner"));
            Assert.AreEqual(0, _games.Fold(_room.Id, 1));
            Assert.AreEqual(0, _games.Fold(_room.Id, 2));
            int results = 0;
            _games.OnRoomRoundEnded += (id, result) =>
            {
                results++;
                Assert.IsTrue(_room.ContainsMember("Owner"));
                Assert.AreEqual(1003, _repository.FindByUserName("Owner").Chips);
                var departures = _departures.CompletePending(id);
                Assert.AreEqual(RoomLeaveStatus.Left, departures.Single().Status);
            };
            Assert.AreEqual(0, _games.NextStage(_room.Id));
            Assert.AreEqual(1, results);
            Assert.IsFalse(_room.ContainsMember("Owner"));
            Assert.AreEqual("Bob", _room.OwnerName);
            Assert.IsNull(_repository.FindByUserName("Owner").LastRoomId);
            Assert.IsEmpty(_departures.CompletePending(_room.Id));
            Assert.AreEqual(0, _games.SynchronizeRoomSeats(_room.Id));
            Assert.AreEqual(0, _games.StartNew(_room.Id));
            Assert.IsFalse(info.Players[0].ParticipatedThisHand);
            Assert.AreEqual(0, info.Players[0].Hole.Count);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void DisconnectAndReconnect_PreservePendingDepartureAndDoNotRestoreFoldedHand()
        {
            _departures.Request(_room.Id, "Owner");
            _games.SetDisconnected(_room.Id, "Owner");
            Assert.IsTrue(_room.FindSeat("Owner").LeaveAfterHand);
            Assert.IsTrue(_games.GetGameInfo(_room.Id).Players[0].IsFold);
            Assert.AreEqual(0, _games.SetConnected(_room.Id, "Owner"));
            Assert.IsTrue(_room.FindSeat("Owner").LeaveAfterHand);
            Assert.IsTrue(_games.GetGameInfo(_room.Id).Players[0].IsFold);
            Assert.AreEqual(2, _games.CountEligiblePlayers(_room.Id));
        }

        [Test]
        public void OfflinePendingPlayer_IsRemovedAfterSettlementUsingAccountPersistence()
        {
            _departures.Request(_room.Id, "Owner");
            _games.SetDisconnected(_room.Id, "Owner");
            Assert.AreEqual(0, _games.Fold(_room.Id, 1));
            Assert.AreEqual(0, _games.NextStage(_room.Id));
            Assert.AreEqual(RoomLeaveStatus.Left, _departures.CompletePending(_room.Id).Single().Status);
            Assert.IsNull(_repository.FindByUserName("Owner").LastRoomId);
            Assert.IsFalse(_rooms.WhichRoom("Owner", out _));
        }

        [Test]
        public void StorageFailure_KeepsPendingPlayerOutOfNextHandUntilRetrySucceeds()
        {
            _departures.Request(_room.Id, "Owner");
            Assert.AreEqual(0, _games.Fold(_room.Id, 0));
            Assert.AreEqual(0, _games.Fold(_room.Id, 1));
            Assert.AreEqual(0, _games.NextStage(_room.Id));
            _storageAvailable = false;
            Assert.AreEqual(RoomLeaveStatus.StorageUnavailable, _departures.CompletePending(_room.Id).Single().Status);
            _rooms.ReleaseWatchingSeats(_room.Id);
            Assert.IsTrue(_room.FindSeat("Owner").LeaveAfterHand);
            Assert.AreEqual(_room.Id, _repository.FindByUserName("Owner").LastRoomId);
            Assert.AreEqual(2, _games.CountEligiblePlayers(_room.Id));
            _storageAvailable = true;
            Assert.AreEqual(RoomLeaveStatus.Left, _departures.CompletePending(_room.Id).Single().Status);
        }

        [Test]
        public void EveryoneQueues_RoomExistsUntilSettlementAndThenDissolves()
        {
            foreach (var name in _room.Players.ToArray())
                Assert.AreEqual(RoomLeaveStatus.Queued, _departures.Request(_room.Id, name));
            Assert.AreEqual(3, _room.MemberCount);
            Assert.IsNotNull(_rooms.FindRoom(_room.Id));
            Assert.AreEqual(0, _games.Fold(_room.Id, 0));
            Assert.AreEqual(0, _games.Fold(_room.Id, 1));
            Assert.AreEqual(0, _games.NextStage(_room.Id));
            Assert.AreEqual(3, _departures.CompletePending(_room.Id).Count);
            Assert.IsNull(_rooms.FindRoom(_room.Id));
            Assert.AreEqual(0, _repository.ReconcileChips().ActiveHandCount);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void InvalidRoomOrMember_CannotChangeMembershipOrPendingState()
        {
            Assert.AreEqual(RoomLeaveStatus.RoomNotFound, _departures.Request(-100, "Owner"));
            Assert.AreEqual(RoomLeaveStatus.NotMember, _departures.Request(_room.Id, "Guest"));
            Assert.IsTrue(_room.Seats.All(seat => !seat.LeaveAfterHand));
        }

        [Test]
        public void Projection_RoundTripsDepartureFlagWithoutDisclosingOtherHoleCards()
        {
            _departures.Request(_room.Id, "Owner");
            var info = new ClientGameInfo(_games.GetGameInfo(_room.Id), 1);
            var json = JsonSerializer.Serialize(info);
            var restored = JsonSerializer.Deserialize<ClientGameInfo>(json);
            Assert.IsTrue(restored.RoomInfo.FindSeat("Owner").LeaveAfterHand);
            Assert.AreEqual(2, restored.Hole.Count);
            Assert.AreEqual(1, json.Split(new[] { "\"Hole\"" }, StringSplitOptions.None).Length - 1);
            Assert.IsFalse(json.Contains("AccountId"));
            Assert.IsFalse(json.Contains("Deck"));
        }
    }
}
