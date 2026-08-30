using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Tests
{
    public class GameManagerRulesTest
    {
        private string _databasePath;
        private SqliteAccountRepository _repository;

        [SetUp]
        public void SetUp()
        {
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                "PokerGameManagerTest-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_databasePath);
            _repository.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(_databasePath);
            DeleteIfExists(_databasePath + "-shm");
            DeleteIfExists(_databasePath + "-wal");
        }

        [Test]
        public void StartNew_HeadsUp_ButtonIsSmallBlindAndActsFirstPreflop()
        {
            var manager = CreateManager(2, out var info);
            info.Dealer = 1;

            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.AreEqual(0, info.Dealer);
            Assert.AreEqual(0, info.SmallBlind);
            Assert.AreEqual(1, info.BigBlind);
            Assert.AreEqual(0, info.OperatingPlayer);
            Assert.AreEqual(50, info.Players[0].StageBet);
            Assert.AreEqual(100, info.Players[1].StageBet);
        }

        [Test]
        public void StartNew_Multiway_FirstActionIsLeftOfBigBlind()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;

            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.AreEqual(0, info.Dealer);
            Assert.AreEqual(1, info.SmallBlind);
            Assert.AreEqual(2, info.BigBlind);
            Assert.AreEqual(0, info.OperatingPlayer);
            Assert.AreEqual(150, info.TotalPot);
            Assert.AreEqual(100, info.CurrentHighestPlayerBet);
            Assert.AreEqual(100, info.CurrentHighestRaise);
            Assert.IsTrue(info.Players.All(player => player.Hole.Count == 2));
        }

        [Test]
        public void Preflop_AllPlayersCall_BigBlindRetainsOptionAndClosesWithCheck()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            manager.StartNew(info.RoomInfo.Id);
            var stageEndedCount = 0;
            manager.OnGameStageEnded += _ => stageEndedCount++;

            Assert.AreEqual(0, manager.Call(info.RoomInfo.Id, 0));
            Assert.AreEqual(1, info.OperatingPlayer);
            Assert.AreEqual(0, manager.Call(info.RoomInfo.Id, 1));
            Assert.AreEqual(2, info.OperatingPlayer);
            Assert.AreEqual(0, manager.Check(info.RoomInfo.Id, 2));

            Assert.AreEqual(-1, info.OperatingPlayer);
            Assert.AreEqual(1, stageEndedCount);
        }

        [Test]
        public void NextStage_ResetsMinimumBetToBigBlindAndBurnsBeforeFlop()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            manager.StartNew(info.RoomInfo.Id);
            manager.Call(info.RoomInfo.Id, 0);
            manager.Call(info.RoomInfo.Id, 1);
            manager.Check(info.RoomInfo.Id, 2);
            var deckCountBeforeFlop = info.Deck.Count;

            Assert.AreEqual(0, manager.NextStage(info.RoomInfo.Id));

            Assert.AreEqual(GameStage.Flop, info.Stage);
            Assert.AreEqual(3, info.Community.Count);
            Assert.AreEqual(deckCountBeforeFlop - 4, info.Deck.Count);
            Assert.AreEqual(0, info.CurrentHighestPlayerBet);
            Assert.AreEqual(100, info.CurrentHighestRaise);
            Assert.AreEqual(info.SmallBlind, info.OperatingPlayer);
            Assert.AreEqual(-5, manager.Bet(info.RoomInfo.Id, info.OperatingPlayer, 99));
            Assert.AreEqual(0, manager.Bet(info.RoomInfo.Id, info.OperatingPlayer, 100));
        }

        [Test]
        public void Call_WithTooFewChips_CommitsStackAsAllInCall()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            SetBalance(info.Players[0], 100);
            manager.StartNew(info.RoomInfo.Id);
            var player = info.Players[info.OperatingPlayer];
            info.CurrentHighestPlayerBet = 200;

            Assert.AreEqual(0, manager.Call(info.RoomInfo.Id, info.OperatingPlayer));

            Assert.AreEqual(0, player.Chips);
            Assert.AreEqual(100, player.StageBet);
            Assert.IsTrue(player.IsAllIn);
            Assert.AreEqual(PlayerOperationType.AllIn, player.LastOperation.Type);
        }

        [Test]
        public void StartNew_SkipsEliminatedPlayerAndSwitchesToHeadsUpPositions()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            SetBalance(info.Players[1], 0);

            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.AreEqual(0, info.Dealer);
            Assert.AreEqual(0, info.SmallBlind);
            Assert.AreEqual(2, info.BigBlind);
            Assert.AreEqual(0, info.OperatingPlayer);
            Assert.IsTrue(info.Players[1].IsFold);
            Assert.AreEqual(0, info.Players[1].Hole.Count);
        }

        [Test]
        public void StartNew_ThreeHandedToHeadsUp_PreviousBigBlindDoesNotPostBigBlindTwice()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 0;
            info.SmallBlind = 1;
            info.BigBlind = 2;
            info.LastHandParticipantCount = 3;
            SetBalance(info.Players[0], 0);

            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.AreEqual(2, info.Dealer);
            Assert.AreEqual(2, info.SmallBlind);
            Assert.AreEqual(1, info.BigBlind);
        }

        [Test]
        public void StartNew_MultiwayUsesDeadButtonAfterElimination()
        {
            var manager = CreateManager(4, out var info);
            info.Dealer = 0;
            info.LastHandParticipantCount = 4;
            SetBalance(info.Players[1], 0);

            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.AreEqual(1, info.Dealer);
            Assert.AreEqual(2, info.SmallBlind);
            Assert.AreEqual(3, info.BigBlind);
            Assert.AreEqual(0, info.OperatingPlayer);
        }

        [Test]
        public void StartNew_PlayerBelowBigBlindDoesNotParticipate()
        {
            var manager = CreateManager(3, out var info, new uint[] { 1000, 30, 1000 });
            info.Dealer = 2;

            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.IsTrue(info.Players[1].IsFold);
            Assert.AreEqual(0, info.Players[1].StageBet);
            Assert.AreEqual(0, info.Players[1].Hole.Count);
        }

        [Test]
        public void StartNew_DatabaseWriteFailureLeavesBalancesUnchangedAndCanRetry()
        {
            var manager = CreateManager(3, out var info);
            manager.SetAccountRepository(new DebitFailureRepository(_repository));

            Assert.AreEqual(-7, manager.StartNew(info.RoomInfo.Id));
            Assert.AreEqual(0, info.TotalPot);
            Assert.IsTrue(info.Players.All(player => _repository.FindById(player.AccountId).Chips == 1000));

            manager.SetAccountRepository(_repository);
            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));
        }

        [Test]
        public void JoinDuringHand_WaitsThenParticipatesInNextHandAtStableSeat()
        {
            var manager = CreateManager(2, out var info, maxPlayerCount: 4);
            info.Dealer = 1;
            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            var newcomer = CreateRepositoryAccount("Newcomer", 1000);
            info.RoomInfo.Seats.Add(new RoomSeatInfo
            {
                SeatIndex = 2,
                PlayerName = newcomer.UserName,
                State = RoomSeatState.WaitingForNextHand,
            });
            info.RoomInfo.SynchronizeLegacyLists();

            Assert.AreEqual(0, manager.SynchronizeRoomSeats(info.RoomInfo.Id));
            Assert.IsTrue(info.Players[2].IsOccupied);
            Assert.IsTrue(info.Players[2].IsWaitingForNextHand);
            Assert.IsFalse(info.Players[2].ParticipatedThisHand);
            Assert.AreEqual(0, info.Players[2].Hole.Count);

            Assert.AreEqual(0, manager.Fold(info.RoomInfo.Id, info.OperatingPlayer));
            Assert.AreEqual(0, manager.NextStage(info.RoomInfo.Id));
            Assert.AreEqual(GameStage.RoundOver, info.Stage);
            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.IsTrue(info.Players[2].ParticipatedThisHand);
            Assert.AreEqual(2, info.Players[2].Hole.Count);
        }

        [Test]
        public void DisconnectDuringHand_AutoActsAndCanRestoreSameSeat()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));
            var disconnectedSeat = info.OperatingPlayer;
            var playerName = info.Players[disconnectedSeat].PlayerName;

            manager.SetDisconnected(info.RoomInfo.Id, playerName);

            Assert.IsTrue(info.Players[disconnectedSeat].IsDisconnected);
            Assert.AreEqual(
                RoomSeatState.DisconnectedInHand,
                info.RoomInfo.FindSeat(disconnectedSeat).State);
            Assert.IsNotNull(info.Players[disconnectedSeat].LastOperation);

            Assert.AreEqual(disconnectedSeat, manager.SetConnected(info.RoomInfo.Id, playerName));
            Assert.IsFalse(info.Players[disconnectedSeat].IsDisconnected);
            Assert.AreEqual(RoomSeatState.Playing, info.RoomInfo.FindSeat(disconnectedSeat).State);
        }

        [Test]
        public void DisconnectDuringHand_AutoChecksWhenFreeAndFoldsWhenFacingBet()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));
            var checkingSeat = info.OperatingPlayer;
            info.Players[checkingSeat].StageBet = info.CurrentHighestPlayerBet;

            manager.SetDisconnected(
                info.RoomInfo.Id,
                info.Players[checkingSeat].PlayerName);

            Assert.IsFalse(info.Players[checkingSeat].IsFold);
            Assert.AreEqual(PlayerOperationType.Check, info.Players[checkingSeat].LastOperation.Type);

            var foldingSeat = info.OperatingPlayer;
            manager.SetDisconnected(
                info.RoomInfo.Id,
                info.Players[foldingSeat].PlayerName);

            Assert.IsTrue(info.Players[foldingSeat].IsFold);
            Assert.AreEqual(PlayerOperationType.Fold, info.Players[foldingSeat].LastOperation.Type);
        }

        [Test]
        public void AllInPlayer_RemainsEligibleAfterDisconnectAndSessionReplacement()
        {
            var manager = CreateManager(3, out var info, new uint[] { 100, 1000, 1000 });
            info.Dealer = 2;
            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));
            Assert.AreEqual(0, info.OperatingPlayer);
            Assert.AreEqual(0, manager.AllIn(info.RoomInfo.Id, 0));

            var playerName = info.Players[0].PlayerName;
            manager.SetDisconnected(info.RoomInfo.Id, playerName);
            Assert.IsTrue(info.Players[0].IsAllIn);
            Assert.IsTrue(info.Players[0].IsDisconnected);
            Assert.IsFalse(info.Players[0].IsFold);

            var sessionManager = new PlayerManager();
            var account = _repository.FindByUserName(playerName);
            var oldConnection = new Mirror.NetworkConnectionToClient(21);
            var replacementConnection = new Mirror.NetworkConnectionToClient(22);
            sessionManager.BindAccount(account, oldConnection);
            Assert.AreSame(oldConnection, sessionManager.BindAccount(account, replacementConnection));
            Assert.IsFalse(sessionManager.RemoveConnection(oldConnection, out _));

            Assert.AreEqual(0, manager.SetConnected(info.RoomInfo.Id, playerName));
            Assert.IsTrue(info.Players[0].IsAllIn);
            Assert.IsFalse(info.Players[0].IsDisconnected);
            Assert.IsFalse(info.Players[0].IsFold);
        }

        [Test]
        public void SettlementFailure_CanRetryWithoutDuplicateCredits()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));
            Assert.AreEqual(0, manager.Fold(info.RoomInfo.Id, 0));
            Assert.AreEqual(0, manager.Fold(info.RoomInfo.Id, 1));

            manager.SetAccountRepository(new SettlementFailureRepository(_repository));
            Assert.AreEqual(-7, manager.NextStage(info.RoomInfo.Id));
            Assert.AreNotEqual(GameStage.RoundOver, info.Stage);
            Assert.AreEqual(150, info.TotalPot);

            manager.SetAccountRepository(_repository);
            Assert.AreEqual(0, manager.NextStage(info.RoomInfo.Id));
            Assert.AreEqual(GameStage.RoundOver, info.Stage);
            Assert.AreEqual(0, info.TotalPot);
            Assert.AreEqual(3000, info.Players.Sum(player => (long)player.Chips));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void SpectatorClientView_HasNoPrivateCardsOrOperationIdentity()
        {
            var manager = CreateManager(2, out var info);
            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            var clientInfo = new ClientGameInfo(info, -1);

            Assert.IsTrue(clientInfo.IsSpectator);
            Assert.AreEqual(-1, clientInfo.CurrentPlayerIndex);
            Assert.AreEqual(0, clientInfo.Hole.Count);
            Assert.IsFalse(clientInfo.CanRaise);
        }

        [Test]
        public void StartNew_WithSeatGapUsesStablePhysicalSeatIndices()
        {
            var manager = CreateManager(3, out var info, maxPlayerCount: 4);
            info.RoomInfo.FindSeat("Player1").SeatIndex = 2;
            info.RoomInfo.FindSeat("Player2").SeatIndex = 3;
            info.RoomInfo.SynchronizeLegacyLists();
            Assert.AreEqual(0, manager.SynchronizeRoomSeats(info.RoomInfo.Id));
            info.Dealer = 3;

            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.AreEqual(0, info.Dealer);
            Assert.AreEqual(2, info.SmallBlind);
            Assert.AreEqual(3, info.BigBlind);
            Assert.AreEqual(0, info.OperatingPlayer);
            Assert.IsFalse(info.Players[1].IsOccupied);
        }

        [Test]
        public void RaiseRights_ShortAllInDoesNotReopenButCumulativeAmountDoes()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            manager.StartNew(info.RoomInfo.Id);
            var player = info.Players[0];
            player.HasActedThisStage = true;
            player.BetFacedAtLastAction = 300;
            info.CurrentHighestRaise = 300;
            info.CurrentHighestPlayerBet = 500;

            Assert.IsFalse(info.CanPlayerRaise(0));

            info.CurrentHighestPlayerBet = 600;
            Assert.IsTrue(info.CanPlayerRaise(0));
        }

        [Test]
        public void Actions_ShortAllInThatDidNotReopenRejectsRaiseAndOversizedAllIn()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            manager.StartNew(info.RoomInfo.Id);
            var player = info.Players[0];
            player.StageBet = 300;
            player.HasActedThisStage = true;
            player.BetFacedAtLastAction = 300;
            info.CurrentHighestRaise = 300;
            info.CurrentHighestPlayerBet = 500;
            info.OperatingPlayer = 0;

            Assert.AreEqual(-6, manager.Bet(info.RoomInfo.Id, 0, 800));
            Assert.AreEqual(-5, manager.AllIn(info.RoomInfo.Id, 0));
        }

        [Test]
        public void Fold_IsLegalWhenCheckingIsAlsoAvailable()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            manager.StartNew(info.RoomInfo.Id);
            var player = info.Players[0];
            player.StageBet = info.CurrentHighestPlayerBet;

            Assert.AreEqual(0, manager.Fold(info.RoomInfo.Id, 0));
            Assert.IsTrue(player.IsFold);
        }

        [Test]
        public void EarlyTermination_AwardsPotConservesChipsAndDoesNotRevealCards()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            manager.StartNew(info.RoomInfo.Id);
            RoundResult result = null;
            manager.OnRoomRoundEnded += (_, roundResult) => result = roundResult;

            Assert.AreEqual(0, manager.Fold(info.RoomInfo.Id, 0));
            Assert.AreEqual(0, manager.Fold(info.RoomInfo.Id, 1));
            Assert.AreEqual(0, manager.NextStage(info.RoomInfo.Id));

            Assert.IsNotNull(result);
            Assert.IsTrue(result.PlayerResults.All(player => player.Hole.Count == 0));
            Assert.AreEqual(3000, info.Players.Sum(player => (long)player.Chips));
            Assert.AreEqual(0, info.TotalPot);
            Assert.AreEqual(GameStage.RoundOver, info.Stage);
        }

        [Test]
        public void Showdown_RevealsOnlyPlayersWhoDidNotFold()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            manager.StartNew(info.RoomInfo.Id);
            info.Players[0].IsFold = true;
            info.Stage = GameStage.River;
            for (int cardIndex = 0; cardIndex < 5; cardIndex++)
                info.Community.Add(info.Deck.Dequeue());
            RoundResult result = null;
            manager.OnRoomRoundEnded += (_, roundResult) => result = roundResult;

            Assert.AreEqual(0, manager.NextStage(info.RoomInfo.Id));

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.PlayerResults[0].Hole.Count);
            Assert.AreEqual(2, result.PlayerResults[1].Hole.Count);
            Assert.AreEqual(2, result.PlayerResults[2].Hole.Count);
        }

        private GameManager CreateManager(
            int playerCount,
            out ServerGameInfo info,
            uint[] startingChips = null,
            int? maxPlayerCount = null)
        {
            var room = new RoomInfo()
            {
                Id = 1000 + playerCount,
                MaxPlayerCount = maxPlayerCount ?? playerCount,
                SmallBlindChips = 50,
                ThinkingTime = 1000,
            };
            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                var playerName = $"Player{playerIndex}";
                room.Players.Add(playerName);
                room.ReadyStatus.Add(true);
                CreateRepositoryAccount(
                    playerName,
                    startingChips == null ? 1000U : startingChips[playerIndex]);
            }

            var manager = new GameManager(_repository);
            Assert.AreEqual(0, manager.CreateGame(room));
            info = manager.GetGameInfo(room.Id);
            return manager;
        }

        private PlayerAccount CreateRepositoryAccount(string playerName, uint chips)
        {
            var now = DateTime.UtcNow;
            var account = new PlayerAccount
            {
                AccountId = Guid.NewGuid().ToString("N"),
                UserName = playerName,
                PasswordAlgorithm = "Test",
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 },
                PasswordIterations = 1,
                Chips = chips,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            Assert.AreEqual(CreateAccountResult.Created, _repository.Create(account));
            return account;
        }

        private void SetBalance(ServerGameInfo.PlayerInfo player, uint chips)
        {
            var current = _repository.FindById(player.AccountId);
            Assert.AreEqual(
                ChipMutationResult.Applied,
                _repository.ApplyChipMutation(
                    player.AccountId,
                    Guid.NewGuid().ToString("N"),
                    "TestBalanceAdjustment",
                    (long)chips - current.Chips,
                    null,
                    null,
                    out var updated));
            player.Chips = updated.Chips;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private sealed class DebitFailureRepository : IHandAccountRepository
        {
            private readonly IHandAccountRepository _inner;

            public DebitFailureRepository(IHandAccountRepository inner)
            {
                _inner = inner;
            }

            public void Initialize() => _inner.Initialize();

            public PlayerAccount FindById(string accountId) => _inner.FindById(accountId);

            public PlayerAccount FindByUserName(string userName) => _inner.FindByUserName(userName);

            public CreateAccountResult Create(PlayerAccount account) => _inner.Create(account);

            public bool SetLastRoom(string accountId, int? roomId) => _inner.SetLastRoom(accountId, roomId);

            public ChipMutationResult ApplyChipMutation(
                string accountId,
                string requestId,
                string transactionType,
                long amount,
                int? roomId,
                string handId,
                out PlayerAccount account)
            {
                return _inner.ApplyChipMutation(
                    accountId,
                    requestId,
                    transactionType,
                    amount,
                    roomId,
                    handId,
                    out account);
            }

            public HandStartResult BeginHand(
                string handId,
                int roomId,
                IReadOnlyList<string> accountIds,
                uint minimumChips,
                out IReadOnlyList<PlayerAccount> accounts)
            {
                return _inner.BeginHand(handId, roomId, accountIds, minimumChips, out accounts);
            }

            public HandDebitResult ApplyHandDebits(
                string handId,
                string requestId,
                IReadOnlyList<HandChipDebit> debits,
                out IReadOnlyDictionary<string, PlayerAccount> accounts)
            {
                throw new InvalidOperationException("Injected database write failure.");
            }

            public HandSettlementResult SettleHand(
                string handId,
                string requestId,
                IReadOnlyList<HandChipCredit> credits,
                out IReadOnlyDictionary<string, PlayerAccount> accounts)
            {
                return _inner.SettleHand(handId, requestId, credits, out accounts);
            }

            public bool AbortHand(string handId, string reason) => _inner.AbortHand(handId, reason);
        }

        private sealed class SettlementFailureRepository : IHandAccountRepository
        {
            private readonly IHandAccountRepository _inner;

            public SettlementFailureRepository(IHandAccountRepository inner)
            {
                _inner = inner;
            }

            public void Initialize() => _inner.Initialize();

            public PlayerAccount FindById(string accountId) => _inner.FindById(accountId);

            public PlayerAccount FindByUserName(string userName) => _inner.FindByUserName(userName);

            public CreateAccountResult Create(PlayerAccount account) => _inner.Create(account);

            public bool SetLastRoom(string accountId, int? roomId) => _inner.SetLastRoom(accountId, roomId);

            public ChipMutationResult ApplyChipMutation(
                string accountId,
                string requestId,
                string transactionType,
                long amount,
                int? roomId,
                string handId,
                out PlayerAccount account)
            {
                return _inner.ApplyChipMutation(
                    accountId,
                    requestId,
                    transactionType,
                    amount,
                    roomId,
                    handId,
                    out account);
            }

            public HandStartResult BeginHand(
                string handId,
                int roomId,
                IReadOnlyList<string> accountIds,
                uint minimumChips,
                out IReadOnlyList<PlayerAccount> accounts)
            {
                return _inner.BeginHand(handId, roomId, accountIds, minimumChips, out accounts);
            }

            public HandDebitResult ApplyHandDebits(
                string handId,
                string requestId,
                IReadOnlyList<HandChipDebit> debits,
                out IReadOnlyDictionary<string, PlayerAccount> accounts)
            {
                return _inner.ApplyHandDebits(handId, requestId, debits, out accounts);
            }

            public HandSettlementResult SettleHand(
                string handId,
                string requestId,
                IReadOnlyList<HandChipCredit> credits,
                out IReadOnlyDictionary<string, PlayerAccount> accounts)
            {
                throw new InvalidOperationException("Injected settlement failure.");
            }

            public bool AbortHand(string handId, string reason) => _inner.AbortHand(handId, reason);
        }
    }

    [TestFixture]
    public class RoomManagerSeatTest
    {
        [Test]
        public void ActiveRoom_UsesStableSeatsAndReusesOnlyFinalizedDepartures()
        {
            var manager = new RoomManager();
            var room = manager.CreateRoom("Owner");
            room.MaxPlayerCount = 4;
            Assert.IsNull(manager.CreateRoom("Owner"));
            Assert.AreEqual(0, manager.Join(room.Id, "Bob"));
            Assert.AreEqual(0, manager.PlayerChangeReady(room.Id, "Bob"));
            Assert.AreEqual(0, manager.StartGame(room.Id));

            Assert.AreEqual(0, manager.Join(room.Id, "Carol"));
            Assert.AreEqual(2, room.FindSeat("Carol").SeatIndex);
            Assert.AreEqual(RoomSeatState.WaitingForNextHand, room.FindSeat("Carol").State);

            Assert.AreEqual(0, manager.Leave(room.Id, "Bob", deferRemoval: true));
            Assert.AreEqual(RoomSeatState.Leaving, room.FindSeat("Bob").State);
            Assert.AreEqual(0, manager.Join(room.Id, "Dave"));
            Assert.AreEqual(3, room.FindSeat("Dave").SeatIndex);

            Assert.IsTrue(manager.FinalizeDepartures(room.Id));
            Assert.AreEqual(0, manager.Join(room.Id, "Erin"));
            Assert.AreEqual(1, room.FindSeat("Erin").SeatIndex);

            Assert.AreEqual(0, manager.Leave(room.Id, "Owner"));
            Assert.AreEqual("Erin", room.OwnerName);
            Assert.IsNotNull(manager.FindRoom(room.Id));
        }

        [Test]
        public void LastMemberLeavingDissolvesRoomImmediately()
        {
            var manager = new RoomManager();
            var room = manager.CreateRoom("OnlyPlayer");
            Assert.AreEqual(0, manager.Join(room.Id, "TemporaryPlayer"));
            Assert.AreEqual(0, manager.Leave(room.Id, "TemporaryPlayer"));
            Assert.IsNotNull(manager.FindRoom(room.Id));

            Assert.AreEqual(0, manager.Leave(room.Id, "OnlyPlayer"));
            Assert.IsNull(manager.FindRoom(room.Id));
        }

        [Test]
        public void WatchingMember_ReleasesSeatAndRestoresAsNextHandPlayer()
        {
            var manager = new RoomManager();
            var room = manager.CreateRoom("Owner");
            room.MaxPlayerCount = 4;
            Assert.AreEqual(0, manager.Join(room.Id, "Bob"));
            Assert.AreEqual(0, manager.PlayerChangeReady(room.Id, "Bob"));
            Assert.AreEqual(0, manager.StartGame(room.Id));
            room.FindSeat("Bob").State = RoomSeatState.Watching;

            Assert.AreEqual(1, manager.ReleaseWatchingSeats(room.Id));
            Assert.IsNull(room.FindSeat("Bob"));
            CollectionAssert.Contains(room.Spectators, "Bob");
            Assert.IsTrue(manager.WhichRoom("Bob", out var associatedRoomId));
            Assert.AreEqual(room.Id, associatedRoomId);

            Assert.AreEqual(0, manager.Restore(room.Id, "Bob", canTakeSeat: true));
            Assert.AreEqual(1, room.FindSeat("Bob").SeatIndex);
            Assert.AreEqual(RoomSeatState.WaitingForNextHand, room.FindSeat("Bob").State);
            CollectionAssert.DoesNotContain(room.Spectators, "Bob");
        }

        [Test]
        public void SpectatorDecliningReturn_RemovesFinalRoomAssociation()
        {
            var manager = new RoomManager();
            var room = manager.CreateRoom("OnlyPlayer");
            Assert.IsTrue(manager.MoveToSpectator(room.Id, "OnlyPlayer"));
            Assert.AreEqual(0, room.PlayerCount);
            Assert.AreEqual(1, room.MemberCount);

            Assert.AreEqual(0, manager.Leave(room.Id, "OnlyPlayer"));
            Assert.IsNull(manager.FindRoom(room.Id));
            Assert.IsFalse(manager.WhichRoom("OnlyPlayer", out _));
        }
    }
}
