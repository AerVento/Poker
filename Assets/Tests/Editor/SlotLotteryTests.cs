using System;
using System.IO;
using System.Linq;
using Game.Network;
using Game.Network.Accounts;
using Mirror;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SlotLotteryTests
    {
        private SqliteAccountRepository _repository;
        private GameCatalog _registry;
        private string _path;
        private string _account;

        [SetUp]
        public void SetUp()
        {
            _path = Path.GetFullPath("Logs/SlotTests/" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            _repository.Initialize();
            _registry = new GameCatalog
            {
                Items = new[]
                {
                    new ItemDefinition { Id = "test-item", Name = "Test", Icon = "test", Consumable = true, MaxQuantity = 10 },
                    new ItemDefinition { Id = "namecard-default", Name = "Default", Icon = "n" },
                    new ItemDefinition { Id = "namecard-test-frame", Name = "Frame", Icon = "f" },
                    new ItemDefinition { Id = "cardface-default", Name = "Classic", Icon = "c" },
                },
                Namecards = new[] { new NamecardDefinition { ItemId = "namecard-default" },
                    new NamecardDefinition { ItemId = "namecard-test-frame", Artwork = "namecards/test-frame" } },
                CardFaces = new[] { new CardFaceDefinition { ItemId = "cardface-default", Preview = "default" } },
            };
            _registry.Validate();
            _account = Create(1000, 500);
        }

        private string Create(uint dollars, uint chips)
        {
            var id = Guid.NewGuid().ToString("N");
            Assert.AreEqual(CreateAccountResult.Created, _repository.Create(new PlayerAccount
            {
                AccountId = id, UserName = "slot_" + id, Dollars = dollars, Chips = chips,
                PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 2 },
                PasswordIterations = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            }));
            return id;
        }

        private SlotLotteryCatalog Catalog(RewardItemKind? kind = null, ulong quantity = 50, string item = null)
        {
            var catalog = new SlotLotteryCatalog
            {
                CostDollars = 100, Revision = "test", NoneWeight = kind.HasValue ? 0 : 1,
                Small = new SlotRewardPool { Weight = kind.HasValue ? 1 : 0 },
                Medium = new SlotRewardPool(), Grand = new SlotRewardPool()
            };
            if (kind.HasValue) catalog.Small.Rewards = new[] { new SlotRewardEntry
            {
                Weight = 1,
                Reward = new Reward(new[] { new RewardEntry { Kind = kind.Value, Quantity = quantity, ItemId = item } })
            } };
            catalog.Validate(_registry.MaxQuantityOf);
            return catalog;
        }

        private SlotDrawExecution Draw(SlotLotteryCatalog catalog, string account = null, string operation = null, string revision = "test")
            => _repository.DrawSlot(account ?? _account, operation ?? Guid.NewGuid().ToString("N"), revision, catalog, _registry);

        [Test]
        public void WeightedIntervalsCoverEveryRollExactly()
        {
            var catalog = Catalog(RewardItemKind.Dollars);
            catalog.NoneWeight = 60;
            catalog.Small.Weight = 30;
            catalog.Medium.Weight = 9;
            catalog.Grand.Weight = 1;
            catalog.Medium.Rewards = catalog.Grand.Rewards = catalog.Small.Rewards;
            Assert.DoesNotThrow(() => catalog.Validate(_registry.MaxQuantityOf));
            var counts = new int[4];
            for (int i = 0; i < 100; i++) counts[(int)catalog.SelectTier(i)]++;
            CollectionAssert.AreEqual(new[] { 60, 30, 9, 1 }, counts);
            catalog.Small.Rewards = new[] {
                new SlotRewardEntry { Weight = 2, Reward = catalog.Medium.Rewards[0].Reward },
                new SlotRewardEntry { Weight = 7, Reward = catalog.Medium.Rewards[0].Reward } };
            CollectionAssert.AreEqual(new[] { 2, 2, 7, 7, 7, 7, 7, 7, 7 },
                Enumerable.Range(0, 9).Select(i => catalog.SelectReward(SlotPrizeTier.Small, i).Weight));
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.SelectTier(100));
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.SelectReward(SlotPrizeTier.Small, -1));
        }

        [Test]
        public void InvalidConfigurationCannotEnableAPool()
        {
            var catalog = Catalog();
            catalog.NoneWeight = 0;
            Assert.Throws<InvalidOperationException>(() => catalog.Validate(_ => null));
            catalog.NoneWeight = 1;
            catalog.Small.Weight = 1;
            Assert.Throws<InvalidOperationException>(() => catalog.Validate(_ => null));
            catalog = Catalog(RewardItemKind.Item, 1, "test-item");
            Assert.Throws<ArgumentException>(() => catalog.Validate(_ => null));
            catalog.Small.Rewards[0].Weight = int.MaxValue;
            catalog.Small.Rewards = new[] { catalog.Small.Rewards[0], new SlotRewardEntry { Weight = 1, Reward = catalog.Small.Rewards[0].Reward } };
            Assert.Throws<InvalidOperationException>(() => catalog.Validate(_ => 10));
        }

        [Test]
        public void DefaultConfigurationLoadsAndRejectsTypos()
        {
            var registry = (System.Func<string, uint?>)CatalogConfiguration.Load().MaxQuantityOf;
            var text = File.ReadAllText(SlotLotteryConfiguration.FilePath);
            var catalog = SlotLotteryConfiguration.Parse(text, registry);
            Assert.AreEqual(1000u, catalog.CostDollars);
            Assert.AreEqual(2000, catalog.TotalWeight);
            Assert.AreEqual(10, catalog.PityDraws);
            Assert.AreEqual(64, catalog.Revision.Length);
            Assert.Throws<InvalidDataException>(() => SlotLotteryConfiguration.Parse(text.Replace("cost-dollars", "cost-dollar"), registry));
        }

        [Test]
        public void LossStillChargesAndReceiptSurvivesReopen()
        {
            var result = Draw(Catalog());
            Assert.AreEqual(SlotLotteryResult.Success, result.Result);
            Assert.AreEqual(SlotPrizeTier.None, result.Receipt.Tier);
            Assert.IsNull(result.Receipt.Reward);
            Assert.AreEqual(900u, _repository.FindById(_account).Dollars);
            var reopened = new SqliteAccountRepository(_path);
            reopened.Initialize();
            Assert.AreEqual(result.Receipt.OperationId, reopened.ReadPendingSlot(_account).OperationId);
        }

        [Test]
        public void ShortfallConvertsOnlyRequiredChips()
        {
            var account = Create(20, 150);
            Assert.AreEqual(SlotLotteryResult.Success, Draw(Catalog(), account).Result);
            var wallet = _repository.ReadWallet(account);
            Assert.AreEqual(0u, wallet.Account.Dollars);
            Assert.AreEqual(70u, wallet.Account.Chips);
        }

        [Test]
        public void InsufficientFundsAndStaleQuoteDoNotCharge()
        {
            var account = Create(20, 50);
            Assert.AreEqual(SlotLotteryResult.InsufficientFunds, Draw(Catalog(), account).Result);
            Assert.AreEqual(20u, _repository.FindById(account).Dollars);
            Assert.AreEqual(50u, _repository.FindById(account).Chips);
            Assert.IsNull(_repository.ReadPendingSlot(account));
            Assert.AreEqual(SlotLotteryResult.ConfigurationChanged, Draw(Catalog(), revision: "stale").Result);
            Assert.AreEqual(1000u, _repository.FindById(_account).Dollars);
        }

        [TestCase(RewardItemKind.Dollars, 950u, 500u)]
        [TestCase(RewardItemKind.Chips, 900u, 550u)]
        public void CurrencyRewardsAreGrantedInSameTransaction(RewardItemKind kind, uint dollars, uint chips)
        {
            Assert.AreEqual(SlotLotteryResult.Success, Draw(Catalog(kind)).Result);
            var wallet = _repository.FindById(_account);
            Assert.AreEqual(dollars, wallet.Dollars);
            Assert.AreEqual(chips, wallet.Chips);
        }

        [Test]
        public void ItemRewardAndDuplicateRequestApplyOnlyOnce()
        {
            var catalog = Catalog(RewardItemKind.Item, 3, "test-item");
            var first = Draw(catalog);
            Assert.IsTrue(first.NewlyApplied);
            Assert.IsFalse(Draw(catalog, operation: first.Receipt.OperationId).NewlyApplied);
            Assert.AreEqual(first.Receipt.OperationId, Draw(catalog).Receipt.OperationId);
            Assert.AreEqual(3u, _repository.ReadItems(_account).Items.Single().Quantity);
            Assert.AreEqual(900u, _repository.FindById(_account).Dollars);
            _repository.AcknowledgeSlot(_account, first.Receipt.OperationId);
            Assert.IsNull(_repository.ReadPendingSlot(_account));
            Assert.IsFalse(Draw(catalog, operation: first.Receipt.OperationId).NewlyApplied);
            Assert.IsTrue(Draw(catalog).NewlyApplied);
            Assert.AreEqual(6u, _repository.ReadItems(_account).Items.Single().Quantity);
        }

        [Test]
        public void AcknowledgementCannotClearAnotherPlayersReceipt()
        {
            var first = Draw(Catalog());
            _repository.AcknowledgeSlot(Create(200, 0), first.Receipt.OperationId);
            Assert.IsNotNull(_repository.ReadPendingSlot(_account));
            Assert.AreEqual(SlotLotteryResult.Success, _repository.DrawSlot(_account, first.Receipt.OperationId,
                "changed", null, null).Result);
        }

        [Test]
        public void RewardThatWouldOverflowIsNotDrawnAndCostIsKept()
        {
            var account = Create(20, 500);
            _repository.GrantItem(account, "test-item", Guid.NewGuid().ToString("N"), 9, _registry);
            Assert.AreEqual(SlotLotteryResult.Success, Draw(Catalog(RewardItemKind.Item, 2, "test-item"), account).Result);
            Assert.AreEqual(0u, _repository.FindById(account).Dollars);
            Assert.AreEqual(420u, _repository.FindById(account).Chips);
            Assert.AreEqual(9u, _repository.ReadItems(account).Items.Single().Quantity);
            var receipt = _repository.ReadPendingSlot(account);
            Assert.AreEqual(SlotPrizeTier.None, receipt.Tier);
            Assert.IsEmpty(receipt.RewardItems());
        }

        private static Reward Items(params (string Id, ulong Quantity)[] items) =>
            new Reward(items.Select(i => new RewardEntry { Kind = RewardItemKind.Item, ItemId = i.Id, Quantity = i.Quantity }).ToArray());

        private SlotLotteryCatalog MixedCatalog()
        {
            var catalog = new SlotLotteryCatalog
            {
                CostDollars = 100, Revision = "test", NoneWeight = 0,
                Small = new SlotRewardPool { Weight = 1, Rewards = new[]
                {
                    new SlotRewardEntry { Weight = 3, Reward = Items(("namecard-test-frame", 1)) },
                    new SlotRewardEntry { Weight = 1, Reward = Items(("test-item", 1), ("namecard-test-frame", 1)) },
                    new SlotRewardEntry { Weight = 2, Reward = Items(("test-item", 1)) },
                } },
                Medium = new SlotRewardPool { Weight = 1, Rewards = new[]
                {
                    new SlotRewardEntry { Weight = 1, Reward = Items(("namecard-test-frame", 1)) },
                } },
                Grand = new SlotRewardPool()
            };
            catalog.Validate(_registry.MaxQuantityOf);
            return catalog;
        }

        [Test]
        public void FitsRejectsABundleWhenAnyItemWouldOverflow()
        {
            var limits = _registry.Items.ToDictionary(i => i.Id, i => i.MaxQuantity);
            var owned = new System.Collections.Generic.Dictionary<string, uint> { ["test-item"] = 8 };
            Assert.IsTrue(SlotLotteryCatalog.Fits(Items(("test-item", 2), ("namecard-test-frame", 1)), owned, limits));
            Assert.IsFalse(SlotLotteryCatalog.Fits(Items(("test-item", 3)), owned, limits));
            owned["namecard-test-frame"] = 1;
            Assert.IsFalse(SlotLotteryCatalog.Fits(Items(("test-item", 1), ("namecard-test-frame", 1)), owned, limits));
            Assert.IsTrue(SlotLotteryCatalog.Fits(Reward.Empty, owned, limits));
        }

        [Test]
        public void EligibleSelectionSkipsOwnedRewardsWithoutGaps()
        {
            var catalog = MixedCatalog();
            Func<Reward, bool> withoutFrame = r => r.Entries.All(e => e.ItemId != "namecard-test-frame");
            Assert.AreEqual(6, catalog.EligibleWeight(SlotPrizeTier.Small));
            Assert.AreEqual(2, catalog.EligibleWeight(SlotPrizeTier.Small, withoutFrame));
            Assert.AreEqual(0, catalog.EligibleWeight(SlotPrizeTier.Medium, withoutFrame));
            Assert.AreEqual(2, catalog.SelectReward(SlotPrizeTier.Small, 0, withoutFrame).Weight);
            Assert.AreEqual(2, catalog.SelectReward(SlotPrizeTier.Small, 1, withoutFrame).Weight);
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.SelectReward(SlotPrizeTier.Small, 2, withoutFrame));
        }

        [Test]
        public void DisclosureShowsOwnedRewardsAsZeroAndMovesEmptyTierToNoPrize()
        {
            var catalog = MixedCatalog();
            Func<Reward, bool> withoutFrame = r => r.Entries.All(e => e.ItemId != "namecard-test-frame");
            var small = SlotLotteryDisclosure.From(catalog, SlotPrizeTier.Small, 0, withoutFrame);
            Assert.AreEqual(2, small.PoolWeight);
            Assert.AreEqual(3, small.TotalRewards);
            CollectionAssert.AreEqual(new[] { true, true, false }, small.Rewards.Select(SlotLotteryDisclosure.IsOwned));
            Assert.AreEqual(1.0, small.PoolProbability(small.Rewards[2]), 1e-9);
            Assert.AreEqual(0.5, small.Probability(SlotPrizeTier.Small), 1e-9);
            Assert.AreEqual(0.5, small.Probability(SlotPrizeTier.None), 1e-9);
            Assert.AreEqual(0.0, small.Probability(SlotPrizeTier.Medium), 1e-9);
            var everyone = SlotLotteryDisclosure.From(catalog, SlotPrizeTier.Small);
            Assert.AreEqual(6, everyone.PoolWeight);
            Assert.IsFalse(everyone.Rewards.Any(SlotLotteryDisclosure.IsOwned));
        }

        [Test]
        public void OwnedNamecardIsNeverDrawnAndOtherRewardsTakeItsShare()
        {
            var account = Create(2000, 0);
            _repository.GrantItem(account, "namecard-test-frame", Guid.NewGuid().ToString("N"), 1, _registry);
            var result = _repository.DrawSlot(account, Guid.NewGuid().ToString("N"), "test", MixedCatalog(), _registry, 10);
            Assert.AreEqual(SlotLotteryResult.Success, result.Result);
            foreach (var outcome in result.Receipt.Results)
            {
                // Medium only holds the owned namecard, so it degrades to no prize; Small always gives the plain item.
                if (outcome.Tier == SlotPrizeTier.None) Assert.IsTrue(outcome.Reward == null || outcome.Reward.Entries.Count == 0);
                else
                {
                    Assert.AreEqual(SlotPrizeTier.Small, outcome.Tier);
                    Assert.AreEqual("test-item", outcome.Reward.Entries.Single().ItemId);
                }
            }
        }

        private SlotLotteryCatalog PityCatalog(int pity = 10)
        {
            // A natural win is about one in a million per draw, so any win before the guarantee would be a real bug.
            var catalog = new SlotLotteryCatalog
            {
                CostDollars = 100, Revision = "test", NoneWeight = 1_000_000, PityDraws = pity,
                Small = new SlotRewardPool { Weight = 1, Rewards = new[] { new SlotRewardEntry { Weight = 1, Reward = Items(("namecard-test-frame", 1)) } } },
                Medium = new SlotRewardPool { Weight = 1, Rewards = new[] { new SlotRewardEntry { Weight = 1,
                    Reward = new Reward(new[] { new RewardEntry { Kind = RewardItemKind.Dollars, Quantity = 7 } }) } } },
                Grand = new SlotRewardPool()
            };
            catalog.Validate(_registry.MaxQuantityOf);
            return catalog;
        }

        [Test]
        public void PityGuaranteesTheTenthDrawOfATenDrawAndResets()
        {
            var account = Create(1000, 0);
            var result = _repository.DrawSlot(account, Guid.NewGuid().ToString("N"), "test", PityCatalog(), _registry, 10);
            Assert.AreEqual(SlotLotteryResult.Success, result.Result);
            Assert.IsTrue(result.Receipt.Results.Take(9).All(o => o.Tier == SlotPrizeTier.None));
            Assert.AreNotEqual(SlotPrizeTier.None, result.Receipt.Results[9].Tier);
            Assert.AreEqual(0, _repository.ReadSlotMisses(account));
        }

        [Test]
        public void PityCountCarriesAcrossSingleDraws()
        {
            var account = Create(1000, 0);
            var catalog = PityCatalog();
            for (int i = 0; i < 9; i++)
            {
                var miss = Draw(catalog, account);
                Assert.AreEqual(SlotPrizeTier.None, miss.Receipt.Tier);
                Assert.AreEqual(i + 1, _repository.ReadSlotMisses(account));
                _repository.AcknowledgeSlot(account, miss.Receipt.OperationId);
            }
            Assert.AreNotEqual(SlotPrizeTier.None, Draw(catalog, account).Receipt.Tier);
            Assert.AreEqual(0, _repository.ReadSlotMisses(account));
        }

        [Test]
        public void GuaranteedDrawSkipsTiersWithNothingLeftToGive()
        {
            var account = Create(1000, 0);
            _repository.GrantItem(account, "namecard-test-frame", Guid.NewGuid().ToString("N"), 1, _registry);
            var result = _repository.DrawSlot(account, Guid.NewGuid().ToString("N"), "test", PityCatalog(), _registry, 10);
            Assert.AreEqual(SlotPrizeTier.Medium, result.Receipt.Results[9].Tier);
            Assert.AreEqual(7UL, result.Receipt.Results[9].Reward.Dollars);
        }

        [Test]
        public void PityDisabledOrOutOfRangeIsHandled()
        {
            var catalog = PityCatalog(0);
            Assert.IsFalse(catalog.IsGuaranteed(500));
            Assert.Throws<InvalidOperationException>(() => PityCatalog(1));
            Assert.Throws<InvalidOperationException>(() => PityCatalog(101));
            var disclosure = SlotLotteryDisclosure.From(PityCatalog());
            Assert.AreEqual(10, disclosure.DrawsUntilGuaranteed);
            disclosure.PityMisses = 9;
            Assert.AreEqual(1, disclosure.DrawsUntilGuaranteed);
            Assert.AreEqual(0, SlotLotteryDisclosure.From(catalog).DrawsUntilGuaranteed);
        }

        [Test]
        public void NamecardWonEarlierInABatchIsExcludedFromLaterDraws()
        {
            var catalog = Catalog(RewardItemKind.Item, 1, "namecard-test-frame");
            var result = _repository.DrawSlot(_account, Guid.NewGuid().ToString("N"), "test", catalog, _registry, 10);
            Assert.AreEqual(SlotLotteryResult.Success, result.Result);
            Assert.AreEqual(SlotPrizeTier.Small, result.Receipt.Results[0].Tier);
            Assert.IsTrue(result.Receipt.Results.Skip(1).All(o => o.Tier == SlotPrizeTier.None));
            Assert.AreEqual(1, result.Receipt.WinCount);
        }

        [Test]
        public void CurrencyOverflowRollsBackCost()
        {
            var account = Create(uint.MaxValue, uint.MaxValue);
            Assert.AreEqual(SlotLotteryResult.RewardLimit, Draw(Catalog(RewardItemKind.Dollars, 101), account).Result);
            Assert.AreEqual(SlotLotteryResult.RewardLimit, Draw(Catalog(RewardItemKind.Chips, 1), account).Result);
            Assert.AreEqual(uint.MaxValue, _repository.FindById(account).Dollars);
        }

        [Test]
        public void NamecardRewardUsesOwnershipAndSkipsDuplicates()
        {
            var catalog = Catalog(RewardItemKind.Item, 1, "namecard-test-frame");
            var first = Draw(catalog);
            Assert.AreEqual(SlotLotteryResult.Success, first.Result);
            // Namecards are ordinary items: one owned row, never stacked.
            Assert.AreEqual(1u, _repository.ReadItems(_account).Items.Single(i => i.ItemId == "namecard-test-frame").Quantity);
            _repository.AcknowledgeSlot(_account, first.Receipt.OperationId);
            var duplicate = Draw(catalog);
            Assert.AreEqual(SlotLotteryResult.Success, duplicate.Result);
            Assert.IsEmpty(duplicate.Receipt.RewardItems());
            Assert.AreEqual(800u, _repository.FindById(_account).Dollars);
        }

        [Test]
        public void TenLossesPayFullCostAndReplayWithoutChargingAgain()
        {
            var catalog = Catalog();
            var operation = Guid.NewGuid().ToString("N");
            var first = _repository.DrawSlot(_account, operation, "test", catalog, _registry, 10);
            Assert.AreEqual(SlotLotteryResult.Success, first.Result);
            Assert.AreEqual(10, first.Receipt.DrawCount);
            Assert.AreEqual(0, first.Receipt.WinCount);
            Assert.AreEqual(1000u, first.Receipt.CostDollars);
            Assert.AreEqual(0u, _repository.FindById(_account).Dollars);
            Assert.IsEmpty(first.Receipt.RewardItems());
            var repeat = _repository.DrawSlot(_account, operation, "changed", null, null, 10);
            Assert.IsFalse(repeat.NewlyApplied);
            Assert.AreEqual(operation, repeat.Receipt.OperationId);
            Assert.AreEqual(10, _repository.ReadPendingSlot(_account).Results.Length);
            Assert.AreEqual(SlotLotteryResult.InvalidRequest, _repository.DrawSlot(_account, operation, "test", catalog, _registry, 1).Result);
            _repository.AcknowledgeSlot(_account, operation);
            Assert.IsNull(_repository.ReadPendingSlot(_account));
            Assert.AreEqual(0u, _repository.FindById(_account).Dollars);
        }

        [Test]
        public void TenDrawsConvertOnlyTheUpfrontShortfall()
        {
            var account = Create(20, 1500);
            var result = _repository.DrawSlot(account, Guid.NewGuid().ToString("N"), "test", Catalog(), _registry, 10);
            Assert.AreEqual(SlotLotteryResult.Success, result.Result);
            Assert.AreEqual(0u, _repository.FindById(account).Dollars);
            Assert.AreEqual(520u, _repository.FindById(account).Chips);
        }

        [Test]
        public void TenRewardsAreGrantedAndDisplayedSeparatelyInDrawOrder()
        {
            var result = _repository.DrawSlot(_account, Guid.NewGuid().ToString("N"), "test", Catalog(RewardItemKind.Dollars), _registry, 10);
            Assert.AreEqual(SlotLotteryResult.Success, result.Result);
            Assert.AreEqual(10, result.Receipt.WinCount);
            Assert.AreEqual(SlotPrizeTier.Small, result.Receipt.Tier);
            Assert.IsTrue(result.Receipt.Results.All(r => r.Reward.Entries.Single().Quantity == 50));
            Assert.AreEqual(10, result.Receipt.RewardItems().Length);
            Assert.IsTrue(result.Receipt.RewardItems().All(r => r.Quantity == 50));
            Assert.AreEqual(500u, _repository.FindById(_account).Dollars);
        }

        [Test]
        public void BatchCannotSpendPrizesBeforePayingAndRejectsInvalidCounts()
        {
            var account = Create(100, 0);
            var catalog = Catalog(RewardItemKind.Dollars, 10000);
            Assert.AreEqual(SlotLotteryResult.InsufficientFunds,
                _repository.DrawSlot(account, Guid.NewGuid().ToString("N"), "test", catalog, _registry, 10).Result);
            foreach (int count in new[] { -1, 0, 2, 9, 11, 255 })
                Assert.AreEqual(SlotLotteryResult.InvalidRequest,
                    _repository.DrawSlot(account, Guid.NewGuid().ToString("N"), "test", catalog, _registry, count).Result);
            Assert.AreEqual(100u, _repository.FindById(account).Dollars);
            Assert.IsNull(_repository.ReadPendingSlot(account));
            catalog.CostDollars = uint.MaxValue;
            Assert.AreEqual(SlotLotteryResult.InsufficientFunds,
                _repository.DrawSlot(account, Guid.NewGuid().ToString("N"), "test", catalog, _registry, 10).Result);
        }

        [Test]
        public void BatchStopsGrantingAtItemLimit()
        {
            var account = Create(20, 1500);
            var result = _repository.DrawSlot(account, Guid.NewGuid().ToString("N"), "test", Catalog(RewardItemKind.Item, 2, "test-item"), _registry, 10);
            Assert.AreEqual(SlotLotteryResult.Success, result.Result);
            Assert.AreEqual(10u, _repository.ReadItems(account).Items.Single().Quantity);
            Assert.AreEqual(0u, _repository.FindById(account).Dollars);
            Assert.AreEqual(520u, _repository.FindById(account).Chips);
            Assert.AreEqual(5, result.Receipt.RewardItems().Length);
            Assert.IsNotNull(_repository.ReadPendingSlot(account));
        }

        [Test]
        public void BatchProtocolKeepsCountAndEveryOutcomeAndReadsLegacyReceipt()
        {
            var result = _repository.DrawSlot(_account, Guid.NewGuid().ToString("N"), "test", Catalog(RewardItemKind.Chips), _registry, 10);
            using var writer = NetworkWriterPool.Get();
            writer.Write(new SlotLotteryRequest { Count = 10, Action = SlotLotteryAction.Draw });
            writer.Write(new SlotLotteryResponse { Receipt = result.Receipt });
            Assert.Less(writer.Position, 16000);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            Assert.AreEqual(10, reader.Read<SlotLotteryRequest>().Count);
            var copy = reader.Read<SlotLotteryResponse>().Receipt;
            Assert.AreEqual(10, copy.Results.Length);
            Assert.AreEqual(10, copy.RewardItems().Length);
            Assert.IsTrue(copy.RewardItems().All(r => r.Quantity == 50));
            var legacy = System.Text.Json.JsonSerializer.Deserialize<SlotDrawReceipt>("{\"CostDollars\":100,\"Tier\":1,\"Reward\":{\"Kind\":0,\"Quantity\":80}}");
            Assert.AreEqual(1, legacy.DrawCount);
            Assert.AreEqual(1, legacy.WinCount);
            Assert.AreEqual(80UL, legacy.RewardItems().Single().Quantity);
        }

        [Test]
        public void RewardAndSlotPrefabsKeepEveryRequiredReference()
        {
            foreach (var path in new[] { "Assets/Prefabs/UI/Panels/SlotMachinePanel/SlotMachinePanel.prefab",
                "Assets/Prefabs/UI/Panels/RewardPopupPanel/RewardPopupPanel.prefab",
                "Assets/Prefabs/UI/Panels/SlotBatchResultPanel/SlotBatchResultPanel.prefab",
                "Assets/Prefabs/UI/Panels/SlotBatchResultPanel/SlotBatchResultView.prefab",
                "Assets/Prefabs/UI/Shared/RewardItemView.prefab", "Assets/Prefabs/UI/Shared/RewardTooltipView.prefab" })
            {
                var root = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
                Assert.IsNotNull(root, path);
                foreach (var component in root.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true))
                {
                    Assert.IsNotNull(component, path + " missing script");
                    if (component.GetType() != typeof(UI.SlotMachinePanel) && component.GetType() != typeof(UI.RewardPopupPanel) &&
                        component.GetType() != typeof(UI.RewardItemView) && component.GetType() != typeof(UI.RewardTooltipView) &&
                        component.GetType() != typeof(UI.SlotBatchResultPanel) && component.GetType() != typeof(UI.SlotBatchResultView)) continue;
                    foreach (var field in component.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                    {
                        if (!field.IsDefined(typeof(UnityEngine.SerializeField), true)) continue;
                        if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                            Assert.IsTrue((UnityEngine.Object)field.GetValue(component) != null, path + " " + field.Name);
                    }
                }
            }
            var manager = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Manager/UIManager.prefab");
            var serialized = new UnityEditor.SerializedObject(manager.GetComponent<Framework.UI.UIManager>());
            var panels = serialized.FindProperty("_panels");
            Assert.IsTrue(Enumerable.Range(0, panels.arraySize).Any(i =>
                (panels.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.GameObject)?.GetComponent<UI.RewardPopupPanel>() != null));
        }

        [Test]
        public void MirrorResponseRoundTripsActualRewardAndNullReceipt()
        {
            foreach (var receipt in new[] { Draw(Catalog(RewardItemKind.Item, 3, "test-item")).Receipt, null })
            {
                using var writer = NetworkWriterPool.Get();
                var source = new SlotLotteryResponse { RequestId = 19, AccountId = _account, Receipt = receipt,
                    CostDollars = 100, Result = SlotLotteryResult.Success, CanExchange = true, AvailableChips = 42 };
                writer.Write(source);
                using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
                var copy = reader.Read<SlotLotteryResponse>();
                Assert.AreEqual(source.RequestId, copy.RequestId);
                Assert.AreEqual(source.AccountId, copy.AccountId);
                Assert.AreEqual(source.AvailableChips, copy.AvailableChips);
                Assert.AreEqual(receipt?.Reward?.Entries.FirstOrDefault()?.ItemId, copy.Receipt?.Reward?.Entries.FirstOrDefault()?.ItemId);
                Assert.AreEqual(receipt?.Reward?.Entries.FirstOrDefault()?.Quantity, copy.Receipt?.Reward?.Entries.FirstOrDefault()?.Quantity);
                Assert.AreEqual(receipt?.OperationId, copy.Receipt?.OperationId);
            }
        }
    }
}
