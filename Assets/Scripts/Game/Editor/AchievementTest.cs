using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Achievements;
using Game.Network;
using Game.Network.Accounts;
using Game.Network.Achievements;
using Game.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class AchievementTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private AchievementService _service;
        private const string Id = "full_house_beats_full_house";

        [SetUp]
        public void Setup()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerAchievement-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            _repository.Initialize();
            foreach (var id in new[] { "Alice", "Bob", "Carol" })
                _repository.Create(new PlayerAccount { AccountId = id, UserName = id, Chips = 10000,
                    PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 1 }, PasswordIterations = 1,
                    CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
            _service = Service(new FullHouseBeatsFullHouseAchievement());
        }

        private AchievementService Service(params Achievement[] rules)
        {
            var service = new AchievementService(_repository, new AchievementCatalog(rules), _ => 2, () => 10000);
            service.Initialize();
            return service;
        }

        [TearDown]
        public void Cleanup()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private object Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open(); using var command = connection.CreateCommand(); command.CommandText = sql;
            return command.ExecuteScalar();
        }

        private static AchievementPlayer Player(string id, int strength, bool folded = false, PokerHandRank rank = PokerHandRank.Full_House)
            => new AchievementPlayer(id, id, id == "Alice" ? 0 : id == "Bob" ? 1 : 2, folded, false, false, 10000, 10000,
                100, 100, Array.Empty<Poker>(), rank, new[] { strength, 2 }, id == "Alice" ? 200u : 0, 0);

        private static AchievementContext Context(string hand = "test", GameStage stage = GameStage.RoundOver,
            bool showdown = true, AchievementPlayer[] players = null, AchievementPot[] pots = null)
        {
            players ??= new[] { Player("Alice", 12), Player("Bob", 11) };
            pots ??= new[] { new AchievementPot(200, new[] { "Alice", "Bob" }, new[] { "Alice" }) };
            return new AchievementContext(hand + ":achievement:" + (int)stage, hand, 1, GameStage.River, stage, 0, 0, 1, 50,
                showdown, 1000, new[] { new AchievementStreet(stage, 200, Array.Empty<Poker>(), players) }, Array.Empty<AchievementAction>(), pots);
        }

        private void Publish(AchievementContext context)
        {
            _repository.PublishAchievementEvent(context);
            _service.DrainEvents();
        }
        private AchievementView State(string account = "Alice") => _service.GetStatus(account).Achievements.Single();

        [Test]
        public void DiscoveryFindsSpecialAchievementAndRejectsDuplicateIds()
        {
            var definition = AchievementCatalog.Discover().Find(Id);
            Assert.IsInstanceOf<FullHouseBeatsFullHouseAchievement>(definition);
            Assert.AreEqual(AchievementCategories.Special, definition.Category);
            Assert.Throws<InvalidOperationException>(() => new AchievementCatalog(new[] { definition, definition }));
        }

        [Test]
        public void CompletesOnlyWinnerAndSurvivesReplayRestartClaimAndLaterHands()
        {
            var context = Context();
            Publish(context);
            Assert.AreEqual(1, State().Progress);
            Assert.AreEqual(0, State("Bob").CompletedAt);
            Assert.AreEqual(1, _service.PendingAnnouncements("Alice").Length);
            Publish(context);
            _service = Service(new FullHouseBeatsFullHouseAchievement());
            Assert.Greater(State().CompletedAt, 0);
            Assert.AreEqual(AchievementResult.Success, _service.Claim("Alice", Id, out _).Result);
            Assert.AreEqual(AchievementResult.AlreadyClaimed, _service.Claim("Alice", Id, out _).Result);
            Publish(Context("next"));
            Assert.AreEqual(11000, _repository.FindById("Alice").Chips);
            Assert.AreEqual(1, State().Progress);
            Assert.Greater(State().ClaimedAt, 0);
            _service.Acknowledge("Alice", Id);
            _service = Service(new FullHouseBeatsFullHouseAchievement());
            Assert.IsEmpty(_service.PendingAnnouncements("Alice"));
            Assert.AreEqual(1L, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='AchievementReward';"));
        }

        [TestCase(GameStage.Preflop, true)]
        [TestCase(GameStage.Flop, true)]
        [TestCase(GameStage.Turn, true)]
        [TestCase(GameStage.River, true)]
        [TestCase(GameStage.RoundOver, false)]
        public void OnlySuccessfullySettledShowdownsQualify(GameStage stage, bool showdown)
        {
            Publish(Context(stage: stage, showdown: showdown));
            Assert.AreEqual(0, State().CompletedAt);
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void FoldedOpponentTieAndFinalQuadsDoNotQualify(bool folded, bool tie, bool quads)
        {
            Publish(Context(players: new[] { Player("Alice", 12, rank: quads ? PokerHandRank.Four_of_a_Kind : PokerHandRank.Full_House),
                Player("Bob", tie ? 12 : 11, folded) }));
            Assert.AreEqual(0, State().CompletedAt);
        }

        [Test]
        public void SidePotsRequireACommonContestedPotButAllowCoWinnerBeatingAThirdPlayer()
        {
            var players = new[] { Player("Alice", 12), Player("Bob", 11), Player("Carol", 12) };
            Publish(Context("unrelated", players: players, pots: new[] {
                new AchievementPot(100, new[] { "Alice" }, new[] { "Alice" }),
                new AchievementPot(100, new[] { "Bob", "Carol" }, new[] { "Carol" }) }));
            Assert.AreEqual(0, State().CompletedAt);
            Publish(Context("split", players: players, pots: new[] {
                new AchievementPot(300, new[] { "Alice", "Bob", "Carol" }, new[] { "Alice", "Carol" }) }));
            Assert.Greater(State().CompletedAt, 0);
            Assert.Greater(State("Carol").CompletedAt, 0);
        }

        private sealed class MixedReward : Achievement
        {
            public override string Id => "mixed";
            public override string Name => "Mixed";
            public override string Description => "Test rewards";
            public override Reward Reward => new Reward(200, new[] { new ItemStack("test_item", 1) });
            public override void Evaluate(AchievementContext context, string accountId, AchievementProgress progress) => progress.Count = 1;
        }

        [Test]
        public void RewardAndClaimStateRollBackTogetherAndConcurrentClaimsPayOnce()
        {
            _service = Service(new MixedReward());
            Publish(Context());
            Sql("CREATE TRIGGER fail_achievement_save BEFORE UPDATE ON achievement_accounts BEGIN SELECT RAISE(ABORT,'save failed'); END;");
            Assert.Throws<SqliteException>(() => _service.Claim("Alice", "mixed", out _));
            Assert.AreEqual(10000, _repository.FindById("Alice").Chips);
            Assert.AreEqual(0L, Sql("SELECT COUNT(*) FROM account_items WHERE account_id='Alice';"));
            Assert.AreEqual(0, State().ClaimedAt);
            Sql("DROP TRIGGER fail_achievement_save;");
            var claims = new[] { Task.Run(() => _service.Claim("Alice", "mixed", out _).Result), Task.Run(() => _service.Claim("Alice", "mixed", out _).Result) };
            Task.WaitAll(claims);
            CollectionAssert.AreEquivalent(new[] { AchievementResult.Success, AchievementResult.AlreadyClaimed }, claims.Select(t => t.Result));
            Assert.AreEqual(10200, _repository.FindById("Alice").Chips);
            Assert.AreEqual(1L, Sql("SELECT quantity FROM account_items WHERE account_id='Alice' AND item_id='test_item';"));
        }

        [Test]
        public void InRoomAndItemLimitLeaveRewardClaimable()
        {
            _service = Service(new MixedReward());
            Publish(Context());
            _repository.SetLastRoom("Alice", 1);
            Assert.AreEqual(AchievementResult.OutsideRoomRequired, _service.Claim("Alice", "mixed", out _).Result);
            _repository.SetLastRoom("Alice", null);
            Sql("INSERT INTO account_items(account_id,item_id,quantity) VALUES('Alice','test_item',2);");
            Assert.AreEqual(AchievementResult.ItemLimit, _service.Claim("Alice", "mixed", out _).Result);
            Assert.AreEqual(0, State().ClaimedAt);
            Assert.AreEqual(10000, _repository.FindById("Alice").Chips);
        }

        [Test]
        public void FailedProgressSaveDoesNotConsumeEvent()
        {
            _repository.PublishAchievementEvent(Context());
            Sql("CREATE TRIGGER fail_progress BEFORE INSERT ON achievement_accounts BEGIN SELECT RAISE(ABORT,'save failed'); END;");
            Assert.Throws<SqliteException>(() => _service.DrainEvents());
            Assert.AreEqual(2, _repository.ReadPendingAchievementEvents(10).Length);
            Sql("DROP TRIGGER fail_progress;");
            _service.DrainEvents();
            Assert.AreEqual(1, State().Progress);
        }

        [Test]
        public void ContextIsImmutableAndRoundTripsWithCardsHistoryAndPotEligibility()
        {
            var cards = new[] { new Poker(PokerSuit.Heart, PokerRank.Ace) };
            var players = new[] { Player("Alice", 12), Player("Bob", 11) };
            var street = new AchievementStreet(GameStage.River, 200, cards, players);
            cards[0] = new Poker(PokerSuit.Club, PokerRank.Two);
            Assert.AreEqual(PokerRank.Ace, street.Community[0].Rank);
            var original = Context();
            var roundTrip = JsonSerializer.Deserialize<AchievementContext>(JsonSerializer.Serialize(original));
            Assert.AreEqual(original.EventId, roundTrip.EventId);
            Assert.AreEqual(12, roundTrip.Players[0].Grades[0]);
            CollectionAssert.AreEqual(new[] { "Alice", "Bob" }, roundTrip.Pots[0].Eligible);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)roundTrip.Pots[0].Winners)[0] = "Bob");
        }

        [Test]
        public void RealGamePublishesEveryStreetAndSettlementBeforeNextHandOrRoomDeletion()
        {
            var room = RoomInfo.CreateRoom("Alice");
            room.Seats.Add(new RoomSeatInfo { SeatIndex = 1, PlayerName = "Bob" });
            room.SynchronizeLegacyLists();
            var manager = new GameManager(_repository);
            Assert.AreEqual(0, manager.CreateGame(room));
            Assert.AreEqual(0, manager.StartNew(room.Id));
            var info = manager.GetGameInfo(room.Id);
            info.Players[0].Hole = new List<Poker> { Card(PokerSuit.Heart, PokerRank.Ace), Card(PokerSuit.Club, PokerRank.Three) };
            info.Players[1].Hole = new List<Poker> { Card(PokerSuit.Heart, PokerRank.King), Card(PokerSuit.Club, PokerRank.Queen) };
            info.Deck = new Queue<Poker>(new[] { Card(PokerSuit.Spade, PokerRank.Four), Card(PokerSuit.Club, PokerRank.Ace),
                Card(PokerSuit.Diamond, PokerRank.Ace), Card(PokerSuit.Club, PokerRank.King), Card(PokerSuit.Spade, PokerRank.Five),
                Card(PokerSuit.Diamond, PokerRank.King), Card(PokerSuit.Spade, PokerRank.Six), Card(PokerSuit.Spade, PokerRank.Two) });
            var hand = info.HandId;
            while (info.Stage < GameStage.RoundOver)
            {
                while (info.OperatingPlayer >= 0)
                {
                    var current = info.OperatingPlayer;
                    var result = info.Players[current].StageBet < info.CurrentHighestPlayerBet ? manager.Call(room.Id, current) : manager.Check(room.Id, current);
                    Assert.AreEqual(0, result);
                }
                Assert.AreEqual(0, manager.NextStage(room.Id));
            }
            var contexts = _repository.ReadPendingAchievementEvents(100).Where(e => e.AccountId == "Alice").Select(e => e.Context).ToArray();
            CollectionAssert.AreEqual(new[] { GameStage.Preflop, GameStage.Flop, GameStage.Turn, GameStage.River, GameStage.RoundOver }, contexts.Select(c => c.Stage));
            Assert.AreEqual(8, contexts.Last().Actions.Count);
            Assert.AreEqual(PokerHandRank.Full_House, contexts.Last().Players[0].BestRank);
            Assert.AreEqual(PokerHandRank.Full_House, contexts.Last().Players[1].BestRank);
            Assert.IsTrue(contexts.Last().Showdown);
            manager.GameOver(room.Id);
            _service.DrainEvents();
            Assert.AreEqual(1, State().Progress);
            Assert.AreEqual(0, State("Bob").Progress);
            Assert.AreEqual(5L, Sql("SELECT COUNT(*) FROM achievement_events WHERE hand_id='" + hand + "';"));
        }

        [TestCase(5, PokerHandRank.Two_Pair)]
        [TestCase(6, PokerHandRank.Full_House)]
        [TestCase(7, PokerHandRank.Four_of_a_Kind)]
        public void BestPatternSupportsFlopTurnAndRiverWithoutRequiringSevenCards(int count, PokerHandRank rank)
        {
            var cards = new[] { Card(PokerSuit.Heart, PokerRank.Ace), Card(PokerSuit.Diamond, PokerRank.Ace),
                Card(PokerSuit.Club, PokerRank.King), Card(PokerSuit.Diamond, PokerRank.King), Card(PokerSuit.Club, PokerRank.Queen),
                Card(PokerSuit.Club, PokerRank.Ace), Card(PokerSuit.Spade, PokerRank.Ace) };
            Assert.AreEqual(rank, AchievementPatternEvaluator.GetBest(cards.Take(count).ToArray()).Rank);
        }

        private static Poker Card(PokerSuit suit, PokerRank rank) => new Poker(suit, rank);
    }
}
