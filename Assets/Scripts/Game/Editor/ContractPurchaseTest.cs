using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class ContractPurchaseTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private PlayerAccount _account;
        private ItemPurchaseRuntime _runtime;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerContracts-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            var service = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            service.Initialize();
            _account = service.Register("ContractUser", "password123").Account;
            _runtime = ContractPurchaseRegistration.CreateRuntime(new ItemServerCatalog
            {
                Products = JsonSerializer.Deserialize<ServerItemDefinition[]>(Resources.Load<TextAsset>("SO/Shop/ItemServerCatalog").text)
            });
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private void Balance(uint balance)
        {
            var current = _repository.FindById(_account.AccountId).Chips;
            _repository.ApplyChipMutation(_account.AccountId, Guid.NewGuid().ToString("N"), "TestAdjustment",
                (long)balance - current, null, null, out _);
        }

        private ItemChangeExecution Buy(string id = "devil-contract", string operation = null, ItemPurchaseRuntime runtime = null,
            string revision = null, IItemRepository repository = null, bool inRoom = false)
        {
            runtime ??= _runtime;
            return (repository ?? _repository).ChangeItem(_account.AccountId, operation ?? Guid.NewGuid().ToString("N"),
                ItemShopAction.Purchase, id, 1, runtime.Find(id)?.Price ?? 0, revision ?? runtime.Revision, runtime, inRoom);
        }

        private long Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static ItemPurchaseRuntime Custom(IPurchaseCondition[] conditions = null, IPurchaseEffect[] effects = null, uint price = 0)
        {
            var registry = new ItemPurchaseRegistry();
            registry.Register("devil-contract", new ItemPurchasePolicy(conditions, effects));
            return new ItemPurchaseRuntime(new ItemServerCatalog { Products = new[] {
                new ServerItemDefinition { Id = "devil-contract", Price = price, Kind = ItemKind.NonConsumable, MaxQuantity = 1 }
            } }, registry);
        }

        [TestCase(ComparisonType.Equal, false, true, false)]
        [TestCase(ComparisonType.NotEqual, true, false, true)]
        [TestCase(ComparisonType.LessThan, true, false, false)]
        [TestCase(ComparisonType.LessThanOrEqual, true, true, false)]
        [TestCase(ComparisonType.GreaterThan, false, false, true)]
        [TestCase(ComparisonType.GreaterThanOrEqual, false, true, true)]
        public void ComparisonsCoverBothSidesAndEquality(ComparisonType comparison, bool below, bool equal, bool above)
        {
            var condition = new BalanceCondition(comparison, 10);
            Assert.AreEqual(below, condition.Check(new PurchaseContext("a", 9, null, 0)));
            Assert.AreEqual(equal, condition.Check(new PurchaseContext("a", 10, null, 0)));
            Assert.AreEqual(above, condition.Check(new PurchaseContext("a", 11, null, 0)));
        }

        [Test]
        public void BoundariesAndInvalidConfigurationAreExplicit()
        {
            Assert.IsTrue(new BalanceCondition(ComparisonType.Equal, 0).Descriptor.Matches(0));
            Assert.IsTrue(new BalanceCondition(ComparisonType.Equal, uint.MaxValue).Descriptor.Matches(uint.MaxValue));
            Assert.Throws<ArgumentException>(() => new BalanceCondition((ComparisonType)255, 0));
            Assert.Throws<ArgumentException>(() => new RewardChipsEffect(0));
            Assert.Throws<OverflowException>(() => new ItemPurchasePolicy(onPurchaseEffects: new[] {
                new RewardChipsEffect(uint.MaxValue), new RewardChipsEffect(1) }));
        }

        [TestCase("devil-contract", 1000u)]
        [TestCase("archdevil-contract", 10000u)]
        [TestCase("satan-contract", 100000u)]
        public void ProductionContractsCanBePurchasedAgainAndPersistOriginalOutcome(string id, uint reward)
        {
            Assert.AreEqual(ItemShopResult.BalanceConditionNotMet, Buy(id).Result);
            Balance(0);
            var operation = Guid.NewGuid().ToString("N");
            var result = Buy(id, operation);
            Assert.AreEqual(ItemShopResult.Success, result.Result);
            Assert.IsTrue(result.IsNewlyApplied);
            Assert.AreEqual(0, result.Outcome.ChargedPrice);
            Assert.AreEqual(reward, result.Outcome.RewardChips);
            Assert.AreEqual(reward, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(1, _repository.ReadItems(_account.AccountId).Items.Single().Quantity);
            _repository.Initialize();
            var replay = Buy(id, operation, inRoom: true);
            Assert.AreEqual(ItemShopResult.Success, replay.Result);
            Assert.IsFalse(replay.IsNewlyApplied);
            Assert.AreEqual(reward, replay.Outcome.RewardChips);
            Balance(0);
            Assert.AreEqual(ItemShopResult.Success, Buy(id).Result);
            Assert.AreEqual(2, _repository.ReadItems(_account.AccountId).Items.Single().Quantity);
            Assert.AreEqual(2, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='ItemPurchaseReward';"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        private ItemChangeExecution Use(string id, string operation = null, string revision = null, bool inRoom = false)
            => _repository.ChangeItem(_account.AccountId, operation ?? Guid.NewGuid().ToString("N"),
                ItemShopAction.Use, id, 1, 0, revision ?? _runtime.Revision, _runtime, inRoom);

        [TestCase("devil-contract", 1100u, 1ul)]
        [TestCase("archdevil-contract", 11000u, 10ul)]
        [TestCase("satan-contract", 110000u, 100ul)]
        public void RepaymentRequiresExactFundsConsumesOneAndCanSignAgain(string id, uint cost, ulong years)
        {
            Balance(0);
            Assert.AreEqual(ItemShopResult.Success, Buy(id).Result);
            Assert.AreEqual(years, _repository.ReadServiceYears(new[] { _account.AccountId })[_account.AccountId]);
            Balance(cost - 1);
            Assert.AreEqual(ItemShopResult.UseConditionNotMet, Use(id).Result);
            Assert.AreEqual(1, _repository.ReadItems(_account.AccountId).Items.Single().Quantity);
            Assert.AreEqual(cost - 1, _repository.FindById(_account.AccountId).Chips);
            Balance(cost);
            var operation = Guid.NewGuid().ToString("N");
            var use = Use(id, operation);
            Assert.AreEqual(ItemShopResult.Success, use.Result);
            Assert.AreEqual(0, use.Outcome.RewardChips);
            Assert.AreEqual(PurchaseEffectKind.DebitChips, use.Outcome.Effects.Single().Kind);
            Assert.AreEqual(cost, use.Outcome.Effects.Single().Amount);
            Assert.AreEqual(0, _repository.FindById(_account.AccountId).Chips);
            Assert.IsEmpty(_repository.ReadItems(_account.AccountId).Items);
            Assert.AreEqual(0, _repository.ReadServiceYears(new[] { _account.AccountId })[_account.AccountId]);
            Assert.AreEqual(ItemShopResult.Success, Buy(id).Result);
            _repository.Initialize();
            Assert.IsFalse(Use(id, operation, inRoom: true).IsNewlyApplied);
            Assert.AreEqual(1, _repository.ReadItems(_account.AccountId).Items.Single().Quantity);
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='ItemUsePayment';"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [TestCase("BEFORE UPDATE ON player_accounts")]
        [TestCase("BEFORE INSERT ON account_items")]
        [TestCase("BEFORE INSERT ON chip_ledger WHEN NEW.transaction_type='ItemUsePayment'")]
        [TestCase("BEFORE INSERT ON item_requests")]
        public void RepaymentWriteFailureRollsBackAllChanges(string trigger)
        {
            Balance(0);
            Buy();
            Balance(1100);
            var version = _repository.FindById(_account.AccountId).Version;
            var ledgerCount = Sql("SELECT COUNT(*) FROM chip_ledger;");
            var receipts = Sql("SELECT COUNT(*) FROM item_requests;");
            Sql("CREATE TRIGGER repayment_fail " + trigger + " BEGIN SELECT RAISE(ABORT,'Injected'); END;");
            Assert.Throws<SqliteException>(() => Use("devil-contract"));
            Assert.AreEqual(1100, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(version, _repository.FindById(_account.AccountId).Version);
            Assert.AreEqual(1, _repository.ReadItems(_account.AccountId).Items.Single().Quantity);
            Assert.AreEqual(ledgerCount, Sql("SELECT COUNT(*) FROM chip_ledger;"));
            Assert.AreEqual(receipts, Sql("SELECT COUNT(*) FROM item_requests;"));
        }

        [Test]
        public void UseRequiresCurrentRulesAndRoomExitAndDoesNotCallPurchaseEffects()
        {
            Balance(0);
            Buy();
            Balance(1100);
            Assert.AreEqual(ItemShopResult.CatalogChanged, Use("devil-contract", revision: "old").Result);
            Assert.AreEqual(ItemShopResult.OutsideRoomRequired, Use("devil-contract", inRoom: true).Result);
            Assert.AreEqual(ItemShopResult.Success, Use("devil-contract").Result);
            Assert.AreEqual(ItemShopResult.NotOwned, Use("devil-contract").Result);
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='ItemPurchaseReward';"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void UseConditionAndEffectAreIndependentAndVersioned()
        {
            var purchases = new ItemPurchaseRegistry();
            purchases.Register("plain", new ItemPurchasePolicy());
            var catalog = new ItemServerCatalog { Products = new[] {
                new ServerItemDefinition { Id = "plain", Kind = ItemKind.Consumable, MaxQuantity = 9 } } };
            var use = new ItemUseRegistry();
            use.Register("plain", new ItemUsePolicy(new[] { new UseBalanceCondition(ComparisonType.GreaterThanOrEqual, 200) },
                new[] { new PayChipsEffect(100) }));
            var runtime = new ItemPurchaseRuntime(catalog, purchases, use);
            Assert.AreEqual(ItemShopResult.UseConditionNotMet, ItemPurchaseRuntime.CheckUse(new ItemUseContext("a", 150, runtime.Find("plain"), 1), true));
            Assert.AreEqual(ItemShopResult.Success, ItemPurchaseRuntime.CheckUse(new ItemUseContext("a", 200, runtime.Find("plain"), 1), true));
            var effectsOnly = new ItemUseRegistry();
            effectsOnly.Register("plain", new ItemUsePolicy(null, new[] { new PayChipsEffect(100) }));
            var other = new ItemPurchaseRuntime(catalog, purchases, effectsOnly);
            Assert.AreNotEqual(runtime.Revision, other.Revision);
            Assert.AreEqual(ItemShopResult.Success, ItemPurchaseRuntime.CheckUse(new ItemUseContext("a", 150, other.Find("plain"), 1), true));
            Assert.Throws<ArgumentException>(() => new ItemUsePolicy(null, null));
        }

        [Test]
        public void OtherUnownedContractsBecomeEligibleAtLaterZeroBalance()
        {
            foreach (var product in _runtime.Products)
            {
                Balance(0);
                Assert.AreEqual(ItemShopResult.Success, Buy(product.Id).Result);
            }
            Assert.AreEqual(3, _repository.ReadItems(_account.AccountId).Items.Length);
        }

        [TestCase(1u)]
        [TestCase(999u)]
        [TestCase(1000u)]
        [TestCase(100000u)]
        [TestCase(uint.MaxValue)]
        public void EveryPositiveBalanceRejectsAllThreeContracts(uint balance)
        {
            Balance(balance);
            foreach (var product in _runtime.Products)
                Assert.AreEqual(ItemShopResult.BalanceConditionNotMet, Buy(product.Id).Result);
            Assert.AreEqual(balance, _repository.FindById(_account.AccountId).Chips);
            Assert.IsEmpty(_repository.ReadItems(_account.AccountId).Items);
        }

        [Test]
        public void MultipleRewardsHaveContinuousLedgerBalancesAndStableIndices()
        {
            var runtime = Custom(effects: new[] { new RewardChipsEffect(100), new RewardChipsEffect(200) }, price: 50);
            var result = Buy(runtime: runtime);
            Assert.AreEqual(ItemShopResult.Success, result.Result);
            Assert.AreEqual(10250, result.Outcome.BalanceAfter);
            Assert.AreEqual(9950, result.Outcome.Effects[0].BalanceBefore);
            Assert.AreEqual(10050, result.Outcome.Effects[1].BalanceBefore);
            CollectionAssert.AreEqual(new[] { 0, 1 }, result.Outcome.Effects.Select(e => e.EffectIndex));
            Assert.AreEqual(2, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='ItemPurchaseReward';"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void ConditionsAndEffectsAreIndependentAndCheckThePreDebitBalance()
        {
            var conditionOnly = Custom(new[] { new BalanceCondition(ComparisonType.Equal, 10000) }, price: 10000);
            Assert.AreEqual(ItemShopResult.Success, Buy(runtime: conditionOnly).Result);
            Assert.AreEqual(0, _repository.FindById(_account.AccountId).Chips);
            Sql("DELETE FROM account_items;");
            var effectOnly = Custom(effects: new[] { new RewardChipsEffect(250) });
            Assert.AreEqual(250, Buy(runtime: effectOnly).Outcome.RewardChips);
            Sql("DELETE FROM account_items;");
            Assert.AreEqual(ItemShopResult.Success, Buy(runtime: Custom()).Result);
            Assert.AreEqual(250, _repository.FindById(_account.AccountId).Chips);
        }

        [Test]
        public void MultipleConditionsAreAndedAndEligibilityNeverPlansEffects()
        {
            var effect = new CountingEffect();
            var runtime = Custom(new[] { new BalanceCondition(ComparisonType.GreaterThan, 0),
                new BalanceCondition(ComparisonType.LessThan, 100) }, new[] { effect });
            Assert.AreEqual(ItemShopResult.BalanceConditionNotMet, Buy(runtime: runtime).Result);
            Assert.AreEqual(0, effect.Calls);
            var context = new PurchaseContext(_account.AccountId, 50, runtime.Find("devil-contract"), 0);
            Assert.AreEqual(ItemShopResult.Success, ItemPurchaseRuntime.CheckPurchase(context, true, out _));
            Assert.AreEqual(0, effect.Calls);
            Balance(50);
            Assert.AreEqual(ItemShopResult.Success, Buy(runtime: runtime).Result);
            Assert.AreEqual(1, effect.Calls);
        }

        [Test]
        public void FailedReceiptIsStableAndChangedRulesRequireANewQuote()
        {
            var operation = Guid.NewGuid().ToString("N");
            var failed = Buy(operation: operation);
            Balance(0);
            var changed = Custom(effects: new[] { new RewardChipsEffect(7) });
            var replay = Buy(operation: operation, runtime: changed, revision: _runtime.Revision);
            Assert.AreEqual(failed.Result, replay.Result);
            Assert.AreEqual(ComparisonType.Equal, replay.Outcome.FailedCondition.Comparison);
            Assert.AreEqual(_runtime.Revision, replay.Outcome.RuleRevision);
            Assert.AreEqual(ItemShopResult.InvalidRequest, Buy(operation: operation, runtime: changed).Result);
            Assert.AreEqual(ItemShopResult.CatalogChanged, Buy(runtime: changed, revision: _runtime.Revision).Result);
            Assert.AreEqual(ItemShopResult.Success, Buy(runtime: changed).Result);
        }

        [Test]
        public void SuccessfulReceiptRetainsItsRewardAfterRulesChange()
        {
            Balance(0);
            var operation = Guid.NewGuid().ToString("N");
            Buy(operation: operation);
            var changed = Custom(effects: new[] { new RewardChipsEffect(7) });
            var replay = Buy(operation: operation, runtime: changed, revision: _runtime.Revision);
            Assert.AreEqual(1000, replay.Outcome.RewardChips);
            Assert.AreEqual(1000, _repository.FindById(_account.AccountId).Chips);
            Assert.IsFalse(replay.IsNewlyApplied);
        }

        [Test]
        public void OverflowAndRoomRejectionsDoNotPlanOrGrant()
        {
            var effect = new CountingEffect();
            var runtime = Custom(effects: new[] { effect });
            Balance(uint.MaxValue);
            Assert.AreEqual(ItemShopResult.BalanceLimit, Buy(runtime: runtime).Result);
            Assert.AreEqual(ItemShopResult.OutsideRoomRequired, Buy(runtime: runtime, inRoom: true).Result);
            Assert.AreEqual(0, effect.Calls);
            Assert.IsEmpty(_repository.ReadItems(_account.AccountId).Items);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [TestCase("BEFORE UPDATE ON player_accounts")]
        [TestCase("BEFORE INSERT ON account_items")]
        [TestCase("BEFORE INSERT ON chip_ledger WHEN NEW.transaction_type='ItemPurchase'")]
        [TestCase("BEFORE INSERT ON chip_ledger WHEN NEW.transaction_type='ItemPurchaseReward'")]
        [TestCase("BEFORE INSERT ON item_requests")]
        public void EveryWriteFailureRollsBackBalanceInventoryLedgerReceiptAndNotification(string trigger)
        {
            Balance(0);
            var beforeVersion = _repository.FindById(_account.AccountId).Version;
            Sql("CREATE TRIGGER contract_fail " + trigger + " BEGIN SELECT RAISE(ABORT,'Injected'); END;");
            var failures = 0;
            var notifications = 0;
            var guarded = new GuardedAccountRepository(_repository, _ => failures++);
            var events = new ItemPurchaseEvents();
            events.Subscribe("devil-contract", _ => notifications++);
            var shop = new ItemShopService(guarded, _runtime, events);
            Assert.Throws<SqliteException>(() => shop.ChangeItem(_account.AccountId, Guid.NewGuid().ToString("N"),
                ItemShopAction.Purchase, "devil-contract", 1, 0, _runtime.Revision));
            Assert.AreEqual(1, failures);
            Assert.AreEqual(0, notifications);
            Assert.AreEqual(0, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(beforeVersion, _repository.FindById(_account.AccountId).Version);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_items;"));
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM item_requests;"));
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type LIKE 'ItemPurchase%';"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void SecondEffectFailureRollsBackTheFirstEffect()
        {
            Balance(0);
            var runtime = Custom(effects: new[] { new RewardChipsEffect(100), new RewardChipsEffect(200) });
            Sql("CREATE TRIGGER second_fail BEFORE INSERT ON chip_ledger WHEN NEW.transaction_type='ItemPurchaseReward' AND NEW.amount=200 BEGIN SELECT RAISE(ABORT,'Injected'); END;");
            Assert.Throws<SqliteException>(() => Buy(runtime: runtime));
            Assert.AreEqual(0, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM item_requests;"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void PostCommitSubscribersAreIsolatedAndNeverReplay()
        {
            Balance(0);
            var errors = 0;
            var notified = 0;
            var other = 0;
            var events = new ItemPurchaseEvents(_ => errors++);
            events.Subscribe("devil-contract", _ => throw new InvalidOperationException("Subscriber"));
            var subscription = events.Subscribe("devil-contract", value =>
            {
                Assert.AreEqual(value.Outcome.BalanceAfter, _repository.FindById(value.AccountId).Chips);
                notified++;
            });
            events.Subscribe("satan-contract", _ => other++);
            var service = new ItemShopService(new GuardedAccountRepository(_repository, _ => Assert.Fail("Storage guard fired")), _runtime, events);
            var operation = Guid.NewGuid().ToString("N");
            for (int i = 0; i < 2; i++) Assert.AreEqual(ItemShopResult.Success, service.ChangeItem(_account.AccountId, operation,
                ItemShopAction.Purchase, "devil-contract", 1, 0, _runtime.Revision).Result);
            Assert.AreEqual(1, errors);
            Assert.AreEqual(1, notified);
            Assert.AreEqual(0, other);
            subscription.Dispose();
            events.Clear();
            Balance(0);
            service.ChangeItem(_account.AccountId, Guid.NewGuid().ToString("N"), ItemShopAction.Purchase,
                "satan-contract", 1, 0, _runtime.Revision);
            Assert.AreEqual(0, other);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ConcurrentRequestsAcrossConnectionsGrantOnlyOnce(bool sameOperation)
        {
            var second = new SqliteAccountRepository(_path);
            second.Initialize();
            Balance(0);
            var operation = Guid.NewGuid().ToString("N");
            var tasks = new[] { Task.Run(() => Buy(operation: operation)), Task.Run(() =>
                Buy(id: sameOperation ? "devil-contract" : "satan-contract", operation: sameOperation ? operation : null, repository: second)) };
            Task.WaitAll(tasks);
            Assert.AreEqual(1, tasks.Count(t => t.Result.IsNewlyApplied));
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM account_items;"));
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='ItemPurchaseReward';"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void RevisionIncludesRulesAndEffectOrderAndRuntimeCopiesAuthoringData()
        {
            Assert.AreNotEqual(Custom().Revision, Custom(new[] { new BalanceCondition(ComparisonType.Equal, 0) }).Revision);
            Assert.AreNotEqual(Custom(new[] { new BalanceCondition(ComparisonType.Equal, 0) }).Revision,
                Custom(new[] { new BalanceCondition(ComparisonType.NotEqual, 0) }).Revision);
            Assert.AreNotEqual(Custom(effects: new[] { new RewardChipsEffect(1), new RewardChipsEffect(2) }).Revision,
                Custom(effects: new[] { new RewardChipsEffect(2), new RewardChipsEffect(1) }).Revision);
            var catalog = new ItemServerCatalog { Products = new[] { new ServerItemDefinition {
                Id = "devil-contract", Kind = ItemKind.NonConsumable, MaxQuantity = 1 } } };
            var registry = new ItemPurchaseRegistry();
            Assert.Throws<InvalidOperationException>(() => new ItemPurchaseRuntime(catalog, registry));
            registry.Register("devil-contract", new ItemPurchasePolicy());
            Assert.Throws<ArgumentException>(() => registry.Register("devil-contract", new ItemPurchasePolicy()));
            var runtime = new ItemPurchaseRuntime(catalog, registry);
            var revision = runtime.Revision;
            catalog.Products[0].Price = 123;
            Assert.AreEqual(0, runtime.Find("devil-contract").Price);
            Assert.AreEqual(revision, runtime.Revision);
        }

        [Test]
        public void Schema7LegacyReceiptMigratesAndReplaysWithoutNewEffects()
        {
            var operation = Guid.NewGuid().ToString("N");
            // Rebuild the schema-7 table, keeping a historical receipt in its original format.
            Sql("DROP TABLE item_requests;");
            Sql("CREATE TABLE item_requests(account_id TEXT NOT NULL,operation_id TEXT NOT NULL,fingerprint TEXT NOT NULL,result INTEGER NOT NULL,created_at_utc TEXT NOT NULL,PRIMARY KEY(account_id,operation_id));");
            var fingerprint = JsonSerializer.Serialize(new { action = ItemShopAction.Purchase, itemId = "devil-contract", quantity = 1u,
                quotedPrice = 0u, catalogRevision = _runtime.Revision });
            using (var connection = new SqliteConnection("Data Source=" + _path))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO item_requests VALUES($a,$o,$f,0,'2026-01-01T00:00:00Z');";
                command.Parameters.AddWithValue("$a", _account.AccountId);
                command.Parameters.AddWithValue("$o", operation);
                command.Parameters.AddWithValue("$f", fingerprint);
                command.ExecuteNonQuery();
            }
            Sql("PRAGMA user_version=7;");
            // Reopening a historical database must not reuse the schema cached before fixture reconstruction.
            SqliteConnection.ClearAllPools();
            _repository.Initialize();
            var result = Buy(operation: operation);
            Assert.AreEqual(ItemShopResult.Success, result.Result);
            Assert.IsFalse(result.IsNewlyApplied);
            Assert.IsNull(result.Outcome);
            Assert.AreEqual(10000, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(SqliteAccountRepository.SchemaVersion, Sql("PRAGMA user_version;"));
        }

        [Test]
        public void MirrorRoundTripsConditionEligibilityAndAppliedOutcome()
        {
            var condition = new PurchaseConditionMessage { Kind = PurchaseConditionKind.Balance,
                Comparison = ComparisonType.GreaterThanOrEqual, Threshold = uint.MaxValue, Version = 1 };
            using var writer = NetworkWriterPool.Get();
            writer.Write(new ItemShopResponse { HasOutcome = true, IsNewlyApplied = true, AppliedRewardChips = 1000,
                HasFailedCondition = true, FailedCondition = condition,
                Offers = new[] { new ItemOfferMessage { ItemId = "devil-contract", Price = 0, RewardChips = 1000,
                    MaxQuantity = 1, PurchaseConditions = new[] { condition }, CanPurchase = false, BlockReason = ItemShopResult.BalanceConditionNotMet } },
                Effects = new[] { new ItemEffectOutcomeMessage { EffectIndex = 2, CommandIndex = 1, Kind = PurchaseEffectKind.CreditChips,
                    Version = 1, Amount = 1000, BalanceBefore = 0, BalanceAfter = 1000 } } });
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            var copy = reader.Read<ItemShopResponse>();
            Assert.IsTrue(copy.HasOutcome && copy.IsNewlyApplied && copy.HasFailedCondition);
            Assert.AreEqual(uint.MaxValue, copy.Offers[0].PurchaseConditions[0].Threshold);
            Assert.AreEqual(ComparisonType.GreaterThanOrEqual, copy.FailedCondition.Comparison);
            Assert.AreEqual(ItemShopResult.BalanceConditionNotMet, copy.Offers[0].BlockReason);
            Assert.AreEqual(1000, copy.AppliedRewardChips);
            Assert.AreEqual(2, copy.Effects[0].EffectIndex);
            Assert.AreEqual(1000, copy.Effects[0].BalanceAfter);
        }

        private sealed class CountingEffect : IPurchaseEffect
        {
            public int Calls;
            public PurchaseEffectDescriptor Descriptor { get; } = new PurchaseEffectDescriptor(PurchaseEffectKind.CreditChips, 1);
            public IReadOnlyList<PurchaseEffectCommand> Plan(PurchaseContext context)
            {
                Calls++;
                return new PurchaseEffectCommand[] { new CreditChips(1) };
            }
        }
    }
}
