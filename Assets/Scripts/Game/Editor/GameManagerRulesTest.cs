using Game.Network;
using NUnit.Framework;
using System.Linq;

namespace Game.Tests
{
    public class GameManagerRulesTest
    {
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
            manager.StartNew(info.RoomInfo.Id);
            var player = info.Players[info.OperatingPlayer];
            player.Chips = 75;
            info.CurrentHighestPlayerBet = 200;

            Assert.AreEqual(0, manager.Call(info.RoomInfo.Id, info.OperatingPlayer));

            Assert.AreEqual(0, player.Chips);
            Assert.AreEqual(75, player.StageBet);
            Assert.IsTrue(player.IsAllIn);
            Assert.AreEqual(PlayerOperationType.AllIn, player.LastOperation.Type);
        }

        [Test]
        public void StartNew_SkipsEliminatedPlayerAndSwitchesToHeadsUpPositions()
        {
            var manager = CreateManager(3, out var info);
            info.Dealer = 2;
            info.Players[1].Chips = 0;

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
            info.Players[0].Chips = 0;

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
            info.Players[1].Chips = 0;

            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.AreEqual(1, info.Dealer);
            Assert.AreEqual(2, info.SmallBlind);
            Assert.AreEqual(3, info.BigBlind);
            Assert.AreEqual(0, info.OperatingPlayer);
        }

        [Test]
        public void StartNew_HeadsUpShortBigBlind_DoesNotForceExcessCall()
        {
            var manager = CreateManager(2, out var info);
            info.Dealer = 1;
            info.Players[1].Chips = 30;

            Assert.AreEqual(0, manager.StartNew(info.RoomInfo.Id));

            Assert.AreEqual(50, info.Players[0].StageBet);
            Assert.AreEqual(30, info.Players[1].StageBet);
            Assert.AreEqual(50, info.CurrentHighestPlayerBet);
            Assert.AreEqual(-1, info.OperatingPlayer);
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

        private static GameManager CreateManager(int playerCount, out ServerGameInfo info)
        {
            var room = new RoomInfo()
            {
                Id = 1000 + playerCount,
                InitialChips = 1000,
                SmallBlindChips = 50,
                ThinkingTime = 1000,
            };
            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                room.Players.Add($"Player{playerIndex}");
                room.ReadyStatus.Add(true);
            }

            var manager = new GameManager();
            manager.CreateGame(room);
            info = manager.GetGameInfo(room.Id);
            return manager;
        }
    }
}
