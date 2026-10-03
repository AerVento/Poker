using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.Achievements;
using Game.Network;
using Game.Network.Accounts;
using Game.Network.Achievements;
using Game.Network.Tasks;
using Game.Tasks;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class UnifiedRewardTests
    {
        private SqliteAccountRepository _repository;
        private string _path;
        private string _account;
        private GameCatalog _catalog;
        private ItemRules _rules;
        private Dictionary<string, uint> _limits;

        private static ItemDefinition Item(string id, bool consumable = false, uint max = 1) =>
            new ItemDefinition { Id = id, Name = id, Icon = id, Consumable = consumable, MaxQuantity = max };

        [SetUp]
        public void SetUp()
        {
            _path = Path.GetFullPath("Logs/RewardRefactor/" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            _repository.Initialize();
            _account = Create(1000, 500);
            _catalog = new GameCatalog
            {
                Items = new[]
                {
                    Item("stack", true, 100), Item("egg", true, 1000), Item("contract", true, uint.MaxValue), Item("trophy"),
                    Item("namecard-default"), Item("namecard-straight-flush"), Item("cardface-default"), Item("cardface-gensokyo"),
                },
                Namecards = new[] { new NamecardDefinition { ItemId = "namecard-default" },
                    new NamecardDefinition { ItemId = "namecard-straight-flush", Artwork = "namecards/straight-flush" } },
                CardFaces = new[] { new CardFaceDefinition { ItemId = "cardface-default", Preview = "default" },
                    new CardFaceDefinition { ItemId = "cardface-gensokyo", Cards = "cardfaces/gensokyo", Preview = "gensokyo" } },
                Gifts = new[] { new GiftDefinition { ItemId = "egg", Effect = "gifts/egg" } },
                Contracts = new[] { new ContractDefinition { ItemId = "contract", SignBelowAssets = 2000, Payout = 1000, Repayment = 1100, ServiceYears = 100 } },
                ServiceNamecards = new[] { new ServiceNamecardDefinition { MinServiceYears = 100, NamecardItemId = "namecard-straight-flush" } },
                Shop = new[]
                {
                    new ShopOffer { ItemId = "egg", Price = 50 }, new ShopOffer { ItemId = "contract", Price = 0 },
                    new ShopOffer { ItemId = "trophy", Price = 10 },
                    new ShopOffer { ItemId = "namecard-straight-flush", Price = 0, Purchasable = false },
                    new ShopOffer { ItemId = "cardface-gensokyo", Price = 0, Purchasable = false },
                },
            };
            _rules = new ItemRules(_catalog);
            _limits = _catalog.Items.ToDictionary(i => i.Id, i => i.MaxQuantity);
        }

        private string Create(uint dollars, uint chips)
        {
            var id = Guid.NewGuid().ToString("N");
            Assert.AreEqual(CreateAccountResult.Created, _repository.Create(new PlayerAccount
            {
                AccountId = id, UserName = "reward_" + id, Dollars = dollars, Chips = chips,
                PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 2 },
                PasswordIterations = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            }));
            return id;
        }

        private static string Operation() => Guid.NewGuid().ToString("N");
        private uint Owned(string itemId) => _repository.ReadItems(_account).Items.FirstOrDefault(i => i.ItemId == itemId)?.Quantity ?? 0;

        private static Reward Bundle() => new Reward(new[] {
            new RewardEntry { Kind = RewardItemKind.Dollars, Quantity = 100 },
            new RewardEntry { Kind = RewardItemKind.Chips, Quantity = 50 },
            new RewardEntry { Kind = RewardItemKind.Item, ItemId = "stack", Quantity = 5 },
            new RewardEntry { Kind = RewardItemKind.Item, ItemId = "namecard-straight-flush", Quantity = 1 },
            new RewardEntry { Kind = RewardItemKind.Item, ItemId = "cardface-gensokyo", Quantity = 1 } });

        private static RewardConfigEntry[] RewardConfigEntries(Reward reward) => reward.Entries
            .Select(e => new RewardConfigEntry { Kind = e.Kind, ItemId = e.ItemId, Quantity = e.Quantity }).ToArray();

        private TaskCatalog Tasks(params TaskConfigEntry[] tasks)
        {
            var definitions = new TaskDefinitionsConfigData();
            definitions.AddRange(tasks);
            var fixedTasks = new FixedTasksConfigData();
            fixedTasks.AddRange(tasks.Select(t => new FixedTaskConfigEntry { TaskId = t.Id }));
            return TaskCatalog.Build(definitions, fixedTasks, new RandomTasksConfigData { Capacity = 0 },
                id => _limits.TryGetValue(id, out var n) ? n : (uint?)null);
        }

        private static TaskConfigEntry Task(string id, Reward reward, int maxCompletions = 1) => new TaskConfigEntry
            { Id = id, Name = id, Type = TaskConfigKind.DirectClaim, Reward = RewardConfigEntries(reward), MaxCompletions = maxCompletions };

        private void SeedInventory()
        {
            Assert.AreEqual(ItemGrantResult.Success, _repository.GrantItem(_account, "stack", Operation(), 98, _catalog).Result);
            Assert.AreEqual(ItemGrantResult.Success, _repository.GrantItem(_account, "namecard-straight-flush", Operation(), 1, _catalog).Result);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothClaimTransactionsCapStacksAndStoreCosmeticsAsItems(bool achievement)
        {
            SeedInventory();
            Reward applied = null;
            if (achievement)
                _repository.TransactAchievements(_account, tx => {
                    Assert.AreEqual(AchievementResult.Success, tx.Grant(Bundle(), _limits, "achievement:test"));
                    applied = tx.GrantedReward; return true; });
            else
                _repository.TransactTasks(_account, tx => {
                    Assert.AreEqual(DailyTaskResult.Success, tx.Grant(Bundle(), _limits, "task:test"));
                    applied = tx.GrantedReward; return true; });
            Assert.AreEqual(100UL, applied.Dollars);
            Assert.AreEqual(50UL, applied.Chips);
            Assert.AreEqual(2u, applied.Items.Single(i => i.ItemId == "stack").Quantity);
            Assert.IsFalse(applied.Items.Any(i => i.ItemId == "namecard-straight-flush"));
            Assert.AreEqual(100u, Owned("stack"));
            Assert.AreEqual(1u, Owned("namecard-straight-flush"));
            Assert.AreEqual(1u, Owned("cardface-gensokyo"));
            Assert.AreEqual(1100u, _repository.FindById(_account).Dollars);
            Assert.AreEqual(550u, _repository.FindById(_account).Chips);
        }

        [TestCase(100u, 2u)]
        [TestCase(200u, 5u)]
        [TestCase(99u, 1u)]
        public void DailyTaskClaimStoresActualRewardAndReplayNeverGrantsAgain(uint currentLimit, uint expected)
        {
            SeedInventory();
            var service = new ConfiguredTaskService(_repository, Tasks(Task("bundle", Bundle())));
            service.Initialize();
            var task = service.GetStatus(_account).Tasks.Single();
            _limits["stack"] = currentLimit;
            var first = service.Claim(_account, task.InstanceId, 1);
            Assert.AreEqual(DailyTaskResult.Success, first.Result);
            Assert.AreEqual(expected, first.Receipt.Reward.Items.Single(i => i.ItemId == "stack").Quantity);
            var repeat = service.Claim(_account, task.InstanceId, 1);
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed, repeat.Result);
            Assert.AreEqual(1100u, _repository.FindById(_account).Dollars);
            Assert.AreEqual(JsonSerializer.Serialize(first.Receipt.Reward), JsonSerializer.Serialize(repeat.Receipt.Reward));
        }

        [TestCase(100u, 2u)]
        [TestCase(200u, 5u)]
        [TestCase(99u, 1u)]
        public void AchievementClaimUsesSnapshotAndActualReward(uint currentLimit, uint expected)
        {
            SeedInventory();
            var definition = new ShowdownStraightFlushAchievement();
            _repository.TransactAchievements(_account, tx => {
                tx.Document.Achievements.Add(new PlayerAchievement { Id = definition.Id, CompletedAt = 1,
                    Reward = Bundle(), ItemLimits = _limits }); tx.Changed = true; return true; });
            _limits["stack"] = currentLimit;
            var service = new AchievementService(_repository, new AchievementCatalog(new[] { definition }), id => _limits.TryGetValue(id, out var n) ? n : (uint?)null);
            var result = service.Claim(_account, definition.Id, out _);
            Assert.AreEqual(AchievementResult.Success, result.Result);
            Assert.AreEqual(100UL, result.GrantedReward.Dollars);
            Assert.AreEqual(expected, result.GrantedReward.Items.Single(i => i.ItemId == "stack").Quantity);
            Assert.AreEqual(AchievementResult.AlreadyClaimed, service.Claim(_account, definition.Id, out _).Result);
            Assert.AreEqual(1100u, _repository.FindById(_account).Dollars);
        }

        [Test]
        public void UnpurchasableCosmeticsCannotBeBoughtButCanBeGrantedAndEquipped()
        {
            foreach (var id in new[] { "namecard-straight-flush", "cardface-gensokyo" })
                Assert.AreEqual(ItemActionResult.UnknownItem,
                    _repository.PurchaseItem(_account, Operation(), id, 1, 0, _rules.Revision, _rules).Result);
            Assert.AreEqual(ItemActionResult.NotOwned, _repository.EquipItem(_account, Operation(), "cardface-gensokyo", _rules));
            _repository.GrantItem(_account, "cardface-gensokyo", Operation(), 10, _catalog);
            Assert.AreEqual(1u, Owned("cardface-gensokyo"));
            Assert.AreEqual(ItemActionResult.Success, _repository.EquipItem(_account, Operation(), "cardface-gensokyo", _rules));
            Assert.AreEqual("cardface-gensokyo", _repository.FindById(_account).EquippedCardFaceId);
            Assert.AreEqual(ItemActionResult.NotEquippable, _repository.EquipItem(_account, Operation(), "egg", _rules));
            _repository.GrantItem(_account, "cardface-default", Operation(), 1, _catalog);
            Assert.AreEqual(ItemActionResult.Success, _repository.EquipItem(_account, Operation(), "cardface-default", _rules));
            // The default appearance is stored as an empty value.
            Assert.AreEqual(string.Empty, _repository.FindById(_account).EquippedCardFaceId);
            Assert.AreEqual(1000u, _repository.FindById(_account).Dollars);
        }

        [Test]
        public void PurchasesUseShopOffersAndRetriesReturnTheReceipt()
        {
            var operation = Operation();
            var first = _repository.PurchaseItem(_account, operation, "egg", 3, 50, _rules.Revision, _rules);
            Assert.AreEqual(ItemActionResult.Success, first.Result);
            Assert.IsTrue(first.IsNewlyApplied);
            Assert.AreEqual(850u, _repository.FindById(_account).Dollars);
            var retry = _repository.PurchaseItem(_account, operation, "egg", 3, 50, _rules.Revision, _rules);
            Assert.AreEqual(ItemActionResult.Success, retry.Result);
            Assert.IsFalse(retry.IsNewlyApplied);
            Assert.AreEqual(3u, Owned("egg"));
            Assert.AreEqual(ItemActionResult.CatalogChanged, _repository.PurchaseItem(_account, Operation(), "egg", 1, 40, _rules.Revision, _rules).Result);
            Assert.AreEqual(ItemActionResult.Success, _repository.PurchaseItem(_account, Operation(), "trophy", 1, 10, _rules.Revision, _rules).Result);
            Assert.AreEqual(ItemActionResult.AlreadyOwned, _repository.PurchaseItem(_account, Operation(), "trophy", 1, 10, _rules.Revision, _rules).Result);
            Assert.AreEqual(ItemActionResult.InvalidRequest, _repository.PurchaseItem(_account, Operation(), "contract", 2, 0, _rules.Revision, _rules).Result);
        }

        [Test]
        public void ContractsFollowContractsCatalog()
        {
            // Total assets 1500 are below the 2000 signing limit: signing pays out 1000.
            var signed = _repository.PurchaseItem(_account, Operation(), "contract", 1, 0, _rules.Revision, _rules);
            Assert.AreEqual(ItemActionResult.Success, signed.Result);
            Assert.AreEqual(1000u, signed.Outcome.RewardChips);
            Assert.AreEqual(2000u, _repository.FindById(_account).Dollars);
            Assert.AreEqual(ItemActionResult.PurchaseConditionNotMet,
                _repository.PurchaseItem(_account, Operation(), "contract", 1, 0, _rules.Revision, _rules).Result);
            var use = new[] { new ItemConsumeCommand("contract", 1) };
            var repaid = _repository.ConsumeItems(_account, Operation(), ItemConsumeMethod.Use, use, _rules.Revision, _rules, ItemUseLocation.OutsideRoom);
            Assert.AreEqual(ItemActionResult.Success, repaid.Result);
            Assert.AreEqual(900u, _repository.FindById(_account).Dollars);
            Assert.AreEqual(0u, Owned("contract"));
            Assert.AreEqual(ItemActionResult.NotOwned,
                _repository.ConsumeItems(_account, Operation(), ItemConsumeMethod.Use, use, _rules.Revision, _rules, ItemUseLocation.OutsideRoom).Result);
        }

        [Test]
        public void ConsumeMethodsAreBoundByFeatureCatalogs()
        {
            _repository.GrantItem(_account, "egg", Operation(), 2, _catalog);
            _repository.GrantItem(_account, "trophy", Operation(), 1, _catalog);
            _repository.GrantItem(_account, "contract", Operation(), 1, _catalog);
            ItemActionResult Consume(ItemConsumeMethod method, string item, ItemUseLocation location, uint quantity = 1, int target = -1) =>
                _repository.ConsumeItems(_account, Operation(), method, new[] { new ItemConsumeCommand(item, quantity, target) },
                    null, _rules, location).Result;
            Assert.AreEqual(ItemActionResult.MethodUnavailable, Consume(ItemConsumeMethod.Use, "egg", ItemUseLocation.OutsideRoom));
            Assert.AreEqual(ItemActionResult.NotConsumable, Consume(ItemConsumeMethod.Use, "trophy", ItemUseLocation.OutsideRoom));
            Assert.AreEqual(ItemActionResult.MethodUnavailable, Consume(ItemConsumeMethod.Give, "contract", ItemUseLocation.StartedRoom, 1, 1));
            Assert.AreEqual(ItemActionResult.RoomStartedRequired, Consume(ItemConsumeMethod.Give, "egg", ItemUseLocation.WaitingRoom, 1, 1));
            // One contract is 100 years of service, which forbids giving gifts.
            Assert.AreEqual(ItemActionResult.ServiceRestricted, Consume(ItemConsumeMethod.Give, "egg", ItemUseLocation.StartedRoom, 1, 1));
            Assert.AreEqual(2u, Owned("egg"));
        }

        [Test]
        public void GiftsComeFromStockFirstAndBuyTheShortfall()
        {
            _repository.GrantItem(_account, "egg", Operation(), 2, _catalog);
            var execution = _repository.ConsumeItems(_account, Operation(), ItemConsumeMethod.Give,
                new[] { new ItemConsumeCommand("egg", 3, 1) }, null, _rules, ItemUseLocation.StartedRoom);
            Assert.AreEqual(ItemActionResult.Success, execution.Result);
            Assert.AreEqual(3u, execution.Applied.Single().Quantity);
            Assert.AreEqual(0u, Owned("egg"));
            Assert.AreEqual(950u, _repository.FindById(_account).Dollars);
        }

        [Test]
        public void StarterGrantsAreGivenToNewAccounts()
        {
            _repository.ConfigureItems(_catalog, new Reward(new[] {
                new RewardEntry { Kind = RewardItemKind.Dollars, Quantity = 10000 },
                new RewardEntry { Kind = RewardItemKind.Item, ItemId = "namecard-default", Quantity = 1 },
                new RewardEntry { Kind = RewardItemKind.Item, ItemId = "cardface-default", Quantity = 1 } }));
            _account = Create(0, 0);
            Assert.AreEqual(10000u, _repository.FindById(_account).Dollars);
            Assert.AreEqual(1u, Owned("namecard-default"));
            Assert.AreEqual(1u, Owned("cardface-default"));
            Assert.AreEqual(0L, _repository.ReconcileChips().AccountLedgerMismatchCount);
        }

        [Test]
        public void AssignedTasksFollowTheConfigurationLoadedAtStartup()
        {
            var first = new ConfiguredTaskService(_repository, Tasks(Task("supply", new Reward(100)), Task("bonus", new Reward(5))));
            first.Initialize();
            Assert.AreEqual(new[] { "supply", "bonus" }, first.GetStatus(_account).Tasks.Select(t => t.DefinitionId).ToArray());

            // After a restart the same assignments show the new reward, completions and order; removed tasks disappear.
            var restarted = new ConfiguredTaskService(_repository, Tasks(Task("other", new Reward(1)), Task("supply", new Reward(300), 2)));
            restarted.Initialize();
            var supply = restarted.GetStatus(_account).Tasks.Single();
            Assert.AreEqual("supply", supply.DefinitionId);
            Assert.AreEqual(300UL, supply.Reward.Dollars);
            Assert.AreEqual(2, supply.MaxCompletions);
            Assert.AreEqual(DailyTaskResult.Success, restarted.Claim(_account, supply.InstanceId, 1).Result);
            Assert.AreEqual(1300u, _repository.FindById(_account).Dollars);
        }

        [Test]
        public void Schema16KeepsOnlyTaskIdsInAssignedTasks()
        {
            const string legacy = "{\"Revision\":3,\"FixedPeriod\":1,\"RandomPeriod\":1,\"Instances\":[{\"InstanceId\":\"a\",\"DefinitionId\":\"supply\"," +
                "\"DefinitionJson\":\"{}\",\"Revision\":\"daily-tasks-v5\",\"DisplayOrder\":4,\"ItemLimits\":{\"egg\":9},\"State\":{\"MaxCompletions\":1}}]," +
                "\"Receipts\":[{\"InstanceId\":\"a\",\"Ordinal\":1,\"Chip\":70,\"Items\":[{\"ItemId\":\"stack\",\"Count\":3}]}],\"DeletedInstanceIds\":[]}";
            using (var connection = new SqliteConnection("Data Source=" + _path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO task_accounts VALUES ($id, $json); PRAGMA user_version = 15;";
                command.Parameters.AddWithValue("$id", _account);
                command.Parameters.AddWithValue("$json", legacy);
                command.ExecuteNonQuery();
            }
            new SqliteAccountRepository(_path).Initialize();
            using (var connection = new SqliteConnection("Data Source=" + _path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT document FROM task_accounts WHERE account_id=$id;";
                command.Parameters.AddWithValue("$id", _account);
                var document = (string)command.ExecuteScalar();
                foreach (var removed in new[] { "DefinitionJson", "daily-tasks-v5", "DisplayOrder", "ItemLimits", "Chip" })
                    StringAssert.DoesNotContain(removed, document);
                var migrated = JsonSerializer.Deserialize<PlayerTaskDocument>(document);
                Assert.AreEqual("supply", migrated.Instances.Single().DefinitionId);
                Assert.AreEqual(70UL, migrated.Receipts.Single().Reward.Dollars);
                Assert.AreEqual(3u, migrated.Receipts.Single().Reward.Items.Single().Quantity);
            }
        }

        [Test]
        public void Schema15MovesLegacyCollectionsIntoItems()
        {
            using (var connection = new SqliteConnection("Data Source=" + _path))
            {
                connection.Open();
                foreach (var sql in new[]
                {
                    "CREATE TABLE account_namecards (account_id TEXT NOT NULL, product_id TEXT NOT NULL, purchased_at_utc TEXT NOT NULL, PRIMARY KEY (account_id, product_id));",
                    "CREATE TABLE account_card_faces (account_id TEXT NOT NULL, product_id TEXT NOT NULL, purchased_at_utc TEXT NOT NULL, PRIMARY KEY (account_id, product_id));",
                    "CREATE TABLE shop_requests (account_id TEXT NOT NULL, operation_id TEXT NOT NULL, fingerprint TEXT NOT NULL, result INTEGER NOT NULL);",
                    "CREATE TABLE card_face_requests (account_id TEXT NOT NULL, operation_id TEXT NOT NULL, fingerprint TEXT NOT NULL, result INTEGER NOT NULL);",
                    "INSERT INTO account_namecards VALUES ($id, 'galaxy', 'x'), ($id, 'default', 'x');",
                    "INSERT INTO account_card_faces VALUES ($id, 'cat-court', 'x'), ($id, 'minimal', 'x');",
                    "INSERT INTO account_items VALUES ($id, 'devil-contract', 2);",
                    "UPDATE player_accounts SET equipped_namecard='galaxy', equipped_card_face='default' WHERE account_id=$id;",
                    "PRAGMA user_version = 14;",
                })
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("$id", _account);
                    command.ExecuteNonQuery();
                }
            }
            var upgraded = new SqliteAccountRepository(_path);
            upgraded.Initialize();
            var items = upgraded.ReadItems(_account).Items.ToDictionary(i => i.ItemId, i => i.Quantity);
            CollectionAssert.AreEquivalent(new[] { "devil-contract", "namecard-galaxy", "namecard-default", "cardface-cat-court",
                "cardface-default", "cardface-minimal" }, items.Keys);
            Assert.AreEqual(2u, items["devil-contract"]);
            Assert.IsTrue(items.Where(p => p.Key != "devil-contract").All(p => p.Value == 1));
            Assert.AreEqual("namecard-galaxy", upgraded.FindById(_account).EquippedNamecardId);
            Assert.AreEqual(string.Empty, upgraded.FindById(_account).EquippedCardFaceId);
            using (var connection = new SqliteConnection("Data Source=" + _path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('account_namecards','account_card_faces','shop_requests','card_face_requests');";
                Assert.AreEqual(0L, (long)command.ExecuteScalar());
                command.CommandText = "PRAGMA user_version;";
                Assert.AreEqual((long)SqliteAccountRepository.SchemaVersion, (long)command.ExecuteScalar());
            }
        }

        [Test]
        public void UnknownItemOrCurrencyOverflowMakesNoPartialChanges()
        {
            foreach (var reward in new[] { Bundle() + new Reward(new[] { new RewardEntry { Kind = RewardItemKind.Item, ItemId = "missing", Quantity = 1 } }),
                Bundle() + new Reward(uint.MaxValue) })
                _repository.TransactTasks(_account, tx => {
                    Assert.AreNotEqual(DailyTaskResult.Success, tx.Grant(reward, _limits, Guid.NewGuid().ToString("N"))); return true; });
            Assert.AreEqual(1000u, _repository.FindById(_account).Dollars);
            Assert.IsEmpty(_repository.ReadItems(_account).Items);
        }

        [Test]
        public void ItemProtocolRoundTripsCompositePayloads()
        {
            using var writer = NetworkWriterPool.Get();
            writer.Write(new AchievementClaimResponse { GrantedReward = Bundle() });
            writer.Write(new DailyTaskClaimResponse { GrantedReward = Bundle() });
            writer.Write(new ShopOfferMessage { ItemId = "cardface-gensokyo", Listed = true, Purchasable = false });
            writer.Write(new ItemConsumeRequest { Method = ItemConsumeMethod.Give,
                Commands = new[] { new ItemConsumeCommandMessage { ItemId = "egg", Quantity = 2, TargetSeatIndex = 3 } } });
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            Assert.AreEqual(JsonSerializer.Serialize(Bundle()), JsonSerializer.Serialize(reader.Read<AchievementClaimResponse>().GrantedReward));
            Assert.AreEqual(50UL, reader.Read<DailyTaskClaimResponse>().GrantedReward.Chips);
            var face = reader.Read<ShopOfferMessage>(); Assert.IsTrue(face.Listed); Assert.IsFalse(face.Purchasable);
            var consume = reader.Read<ItemConsumeRequest>();
            Assert.AreEqual(ItemConsumeMethod.Give, consume.Method);
            Assert.AreEqual(3, consume.Commands.Single().TargetSeatIndex);
        }

        [Test]
        public void LargestCompositeSlotReceiptFitsTransport()
        {
            var reward = new Reward(Enumerable.Range(0, 8).Select(i => new RewardEntry
                { Kind = RewardItemKind.Item, ItemId = "namecard-" + new string('a', 46) + i, Quantity = uint.MaxValue }));
            var catalog = new SlotLotteryCatalog { CostDollars = 100, Small = new SlotRewardPool { Weight = 1,
                Rewards = new[] { new SlotRewardEntry { Weight = 1, Reward = reward } } },
                Medium = new SlotRewardPool(), Grand = new SlotRewardPool() };
            catalog.Validate(_ => 1);
            var receipt = new SlotDrawReceipt { OperationId = Guid.NewGuid().ToString("N"), CostDollars = 1000,
                Results = Enumerable.Range(0, 10).Select(_ => new SlotDrawOutcome { Tier = SlotPrizeTier.Small, Reward = reward }).ToArray() };
            using var writer = NetworkWriterPool.Get();
            writer.Write(new SlotLotteryResponse { Receipt = receipt, Disclosure = SlotLotteryDisclosure.From(catalog),
                AccountId = new string('a', 32), Revision = new string('b', 64) });
            Assert.Less(writer.Position, 16000);
            catalog.Small.Rewards[0].Reward = reward + new Reward(1);
            Assert.Throws<InvalidOperationException>(() => catalog.Validate(_ => 1));
        }

        [Test]
        public void StoredRewardsWriteEntriesAndReadSingleEntryReceipts()
        {
            var json = JsonSerializer.Serialize(Bundle());
            StringAssert.Contains("Entries", json);
            Assert.AreEqual(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<Reward>(json)));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Reward>("{\"chip\":123,\"items\":[]}"));
            var oldSlot = JsonSerializer.Deserialize<SlotDrawReceipt>("{\"Reward\":{\"Kind\":2,\"ItemId\":\"stack\",\"Quantity\":3}}");
            Assert.AreEqual(3u, oldSlot.Reward.Items.Single().Quantity);
        }

        [Test]
        public void PersistedLegacyAchievementReceiptsAreReadable()
        {
            _repository.InitializeTasks(); _repository.InitializeAchievements();
            using (var connection = new SqliteConnection("Data Source=" + _path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO achievement_accounts(account_id,document) VALUES($id,$json)";
                command.Parameters.AddWithValue("$id", _account);
                command.Parameters.AddWithValue("$json", "{\"Achievements\":[{\"Id\":\"old\",\"CompletedAt\":1,\"RewardChips\":77,\"RewardItems\":[{\"ItemId\":\"stack\",\"Quantity\":2}]}]}");
                command.ExecuteNonQuery();
            }
            _repository.TransactAchievements(_account, tx => { Assert.AreEqual(77UL, tx.Document.Achievements.Single().Reward.Dollars);
                Assert.AreEqual(2u, tx.Document.Achievements.Single().Reward.Items.Single().Quantity); return true; });
        }
    }
}
