using System;
using System.Collections.Generic;
using System.Linq;
using Game.Achievements;
using Game.Events;
using Game.Network;
using Game.Network.Accounts;
using Game.Network.Achievements;
using Game.Tasks;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class HiddenAchievementTest
    {
        [TestCase(50000u, 50000u, 0u, PlayerOperationType.AllIn, false, false, true)]
        [TestCase(49999u, 50000u, 0u, PlayerOperationType.AllIn, false, false, false)]
        [TestCase(60000u, 60000u, 10001u, PlayerOperationType.AllIn, false, false, false)]
        [TestCase(60000u, 60000u, 10000u, PlayerOperationType.AllIn, false, false, true)]
        [TestCase(60000u, 60000u, 0u, PlayerOperationType.Raise, false, false, false)]
        [TestCase(60000u, 60000u, 0u, PlayerOperationType.AllIn, true, false, false)]
        [TestCase(60000u, 60000u, 0u, PlayerOperationType.AllIn, false, true, false)]
        public void AllInToAshesUsesExplicitEffectiveAllInAndNoWonPot(uint stackBefore, uint committed,
            uint refund, PlayerOperationType type, bool disconnected, bool wonPot, bool expected)
        {
            var progress = new AchievementProgress();
            new AllInToAshesAchievement().Evaluate(Context(stackBefore, committed, refund, type, disconnected, wonPot),
                "Alice", progress);
            Assert.AreEqual(expected ? 1 : 0, progress.Count);
        }

        [Test]
        public void RevealedCardsSupplementOnlyMatchingShowdownPatternAchievement()
        {
            var pair = new ShowdownPairAchievement();
            var straight = new ShowdownStraightAchievement();
            var pairProgress = new AchievementProgress();
            var straightProgress = new AchievementProgress();
            var revealed = TableEventSource.Revealed("hand", "Alice", new[]
            {
                Card(PokerSuit.Heart, PokerRank.Ace),
                Card(PokerSuit.Club, PokerRank.Seven),
                Card(PokerSuit.Diamond, PokerRank.Five)
            }, new[]
            {
                Card(PokerSuit.Spade, PokerRank.Ace),
                Card(PokerSuit.Club, PokerRank.King)
            }, 1000);

            pair.Evaluate(revealed, "Alice", pairProgress);
            straight.Evaluate(revealed, "Alice", straightProgress);

            Assert.AreEqual(1, pairProgress.Count);
            Assert.AreEqual(0, straightProgress.Count);
        }

        [Test]
        public void ClientCacheKeepsHiddenMetadataMaskedUntilCompletion()
        {
            var cache = new AchievementClientCache();
            cache.BeginSession("server", "Alice");
            Assert.IsTrue(cache.StoreCatalog(new[]
            {
                new AchievementDefinitionView
                {
                    Id = "all_in_to_ashes",
                    Name = HiddenAchievementPresentation.Name,
                    Description = HiddenAchievementPresentation.Description,
                    Category = AchievementCategories.Special,
                    IconPath = HiddenAchievementPresentation.IconPath,
                    Hidden = true,
                    RewardItems = Array.Empty<TaskRewardItemState>()
                }
            }, "catalog"));
            Assert.IsTrue(cache.StoreStates(new[]
            {
                new AchievementStateView { Id = "all_in_to_ashes", RewardItems = Array.Empty<TaskRewardItemState>() }
            }, "catalog", 1, cache.ChangeVersion));

            var hidden = cache.BuildViews().Single();
            Assert.IsTrue(hidden.Hidden);
            Assert.AreEqual(HiddenAchievementPresentation.Name, hidden.Name);
            Assert.AreEqual(0, hidden.Target);
            Assert.AreEqual(0, hidden.RewardDollars);

            cache.ApplyUnlocked(new AchievementView
            {
                Id = "all_in_to_ashes",
                Name = "一掷成灰",
                Description = "全下前持有至少 $50,000 筹码，且全下净投入至少 $50,000，最终未赢得任何奖池。",
                Category = AchievementCategories.Special,
                IconPath = "UI/Achievements/Ashes-v1",
                Hidden = true,
                Progress = 1,
                Target = 1,
                CompletedAt = 1000,
                RewardDollars = 10000,
                RewardItems = Array.Empty<TaskRewardItemState>()
            });

            var revealed = cache.BuildViews().Single();
            Assert.AreEqual("一掷成灰", revealed.Name);
            Assert.AreEqual(1, revealed.Target);
            Assert.AreEqual(10000, revealed.RewardDollars);

            cache.BeginSession("server", "Bob");
            Assert.IsTrue(cache.StoreStates(new[]
            {
                new AchievementStateView { Id = "all_in_to_ashes", RewardItems = Array.Empty<TaskRewardItemState>() }
            }, "catalog", 1, cache.ChangeVersion));
            var otherAccount = cache.BuildViews().Single();
            Assert.AreEqual(HiddenAchievementPresentation.Name, otherAccount.Name);
            Assert.AreEqual(0, otherAccount.RewardDollars);
        }

        [Test]
        public void BatchClaimAwardsEveryEligibleEntryOnce()
        {
            var repository = new FakeAchievementRepository();
            repository.Document.Achievements.AddRange(new[]
            {
                Completed("all_in_to_ashes", 10000),
                Completed("second_hidden_test", 5000)
            });
            var service = new AchievementService(repository,
                new AchievementCatalog(new Achievement[] { new AllInToAshesAchievement(), new SecondHiddenAchievement() }),
                _ => null, () => 2000);

            var first = service.ClaimAll("Alice", AchievementCategories.Special, out var account);
            var second = service.ClaimAll("Alice", AchievementCategories.Special, out _);

            Assert.AreEqual(AchievementResult.Success, first.Result);
            Assert.AreEqual(2, first.ClaimedCount);
            Assert.AreEqual(15000, first.GrantedDollars);
            Assert.AreEqual(15100, account.Dollars);
            Assert.AreEqual(AchievementResult.AlreadyClaimed, second.Result);
            Assert.AreEqual(1, repository.GrantCount);
        }

        private static PlayerAchievement Completed(string id, uint dollars) => new PlayerAchievement
        {
            Id = id,
            CompletedAt = 1000,
            RewardChips = dollars,
            RewardItems = Array.Empty<ItemStack>(),
            ItemLimits = new Dictionary<string, uint>()
        };

        private static AchievementContext Context(uint stackBefore, uint committed, uint refund,
            PlayerOperationType type, bool disconnected, bool wonPot)
        {
            var players = new[]
            {
                new AchievementPlayer("Alice", "Alice", 0, false, true, disconnected, stackBefore, 0, committed,
                    committed, Array.Empty<Poker>(), null, Array.Empty<int>(), 0, refund),
                new AchievementPlayer("Bob", "Bob", 1, false, false, false, committed, committed, 0, committed,
                    Array.Empty<Poker>(), null, Array.Empty<int>(), wonPot ? 0u : committed, 0)
            };
            var action = new AchievementAction("Alice", GameStage.Preflop, 1, type, committed, 0,
                disconnected, stackBefore, committed);
            var pots = new[] { new AchievementPot(committed, new[] { "Alice", "Bob" },
                new[] { wonPot ? "Alice" : "Bob" }) };
            return new AchievementContext("hand:achievement:5", "hand", 1, GameStage.River, GameStage.RoundOver,
                0, 0, 1, 50, false, 1000,
                new[] { new AchievementStreet(GameStage.RoundOver, committed, Array.Empty<Poker>(), players) },
                new[] { action }, pots);
        }

        private static Poker Card(PokerSuit suit, PokerRank rank) => new Poker(suit, rank);

        private sealed class SecondHiddenAchievement : Achievement
        {
            public override string Id => "second_hidden_test";
            public override string Name => "Second";
            public override string Description => "Second hidden test";
            public override string Category => AchievementCategories.Special;
            public override bool Hidden => true;
            public override Reward Reward => new Reward(5000);
        }

        private sealed class FakeAchievementRepository : IAchievementRepository
        {
            public readonly AchievementDocument Document = new AchievementDocument();
            public readonly PlayerAccount Account = new PlayerAccount { AccountId = "Alice", UserName = "Alice", Dollars = 100 };
            public int GrantCount { get; private set; }

            public void InitializeAchievements() { }
            public void PublishAchievementEvent(AchievementContext context) { }
            public PendingAchievementEvent[] ReadPendingAchievementEvents(int limit) => Array.Empty<PendingAchievementEvent>();

            public T TransactAchievements<T>(string accountId, Func<AchievementAccountTransaction, T> operation,
                string eventId = null)
            {
                var transaction = new FakeTransaction(this);
                var result = operation(transaction);
                if (transaction.Changed) Document.Revision++;
                return result;
            }

            private sealed class FakeTransaction : AchievementAccountTransaction
            {
                private readonly FakeAchievementRepository _owner;
                public override bool CanClaim => true;

                public FakeTransaction(FakeAchievementRepository owner)
                {
                    _owner = owner;
                    Account = owner.Account;
                    Document = owner.Document;
                }

                public override AchievementResult Grant(Reward reward, IReadOnlyDictionary<string, uint> limits,
                    string claimKey)
                {
                    Account.Dollars = checked(Account.Dollars + reward.Chip);
                    _owner.GrantCount++;
                    Changed = true;
                    return AchievementResult.Success;
                }
            }
        }
    }
}
