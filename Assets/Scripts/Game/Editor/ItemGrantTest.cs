using System;
using System.IO;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ItemGrantTest
    {
        private string _directory;
        private string _path;
        private SqliteAccountRepository _repository;
        private PlayerAccount _account;
        private NamecardServerCatalog _namecards;
        private ItemIdentityRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "PokerGrants-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "accounts.db");
            _repository = new SqliteAccountRepository(_path);
            var service = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            service.Initialize();
            _account = service.Register("GrantOwner", "password123").Account;
            _namecards = new NamecardServerCatalog { Products = new[]
            {
                new NamecardOffer { Id = "default" },
                new NamecardOffer { Id = "galaxy", Price = 6000 },
                new NamecardOffer { Id = "neon", Price = 9000, Listed = false }
            }};
            _registry = new ItemIdentityRegistry(_namecards, new ItemServerCatalog { Products = new[]
            {
                new ServerItemDefinition { Id = "devil-contract", Kind = ItemKind.NonConsumable },
                new ServerItemDefinition { Id = "token", Kind = ItemKind.Consumable, MaxQuantity = 3 }
            }});
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private ItemGrantExecution Grant(string id = "namecard-galaxy", string operation = null, uint quantity = 1)
            => _repository.GrantItem(_account.AccountId, id, operation ?? Guid.NewGuid().ToString("N"), quantity, _registry);

        private long Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        [Test]
        public void NamecardGrantUsesExistingOwnershipAndNeverAppearsInBackpack()
        {
            var ledgerCountBefore = Sql("SELECT COUNT(*) FROM chip_ledger;");
            var granted = Grant();
            Assert.AreEqual(ItemGrantResult.Success, granted.Result);
            Assert.IsTrue(granted.IsNewlyApplied);
            CollectionAssert.Contains(_repository.ReadNamecards(_account.AccountId).OwnedIds, "galaxy");
            Assert.IsEmpty(_repository.ReadItems(_account.AccountId).Items);
            Assert.AreEqual(10000u, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual("", _repository.FindById(_account.AccountId).EquippedNamecardId);
            Assert.AreEqual(ledgerCountBefore, Sql("SELECT COUNT(*) FROM chip_ledger;"), "Grants must not add chip ledger entries.");
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM shop_requests;"));
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM item_grant_receipts;"));
            Assert.AreEqual(ShopResult.AlreadyOwned, _repository.ChangeNamecard(_account.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Purchase, "galaxy", _namecards.Revision, _namecards));
            Assert.AreEqual(ShopResult.Success, _repository.ChangeNamecard(_account.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Equip, "galaxy", _namecards.Revision, _namecards));
            Assert.AreEqual("galaxy", new SqliteAccountRepository(_path).FindById(_account.AccountId).EquippedNamecardId);
        }

        [Test]
        public void PurchasedNamecardAndDefaultAreAlreadyOwnedWithoutAdditionalRows()
        {
            Assert.AreEqual(ShopResult.Success, _repository.ChangeNamecard(_account.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Purchase, "galaxy", _namecards.Revision, _namecards));
            Assert.AreEqual(ItemGrantResult.AlreadyOwned, Grant().Result);
            Assert.AreEqual(ItemGrantResult.AlreadyOwned, Grant("namecard-default").Result);
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM account_namecards;"));
            Assert.AreEqual(4000u, _repository.FindById(_account.AccountId).Chips);
        }

        [Test]
        public void DelistedNamecardCanBeGrantedAndEquipped()
        {
            Assert.AreEqual(ItemGrantResult.Success, Grant("namecard-neon").Result);
            Assert.AreEqual(ShopResult.Success, _repository.ChangeNamecard(_account.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Equip, "neon", _namecards.Revision, _namecards));
        }

        [Test]
        public void ReplaySurvivesRestartAndCatalogRemovalButChangedPayloadIsRejected()
        {
            var operation = Guid.NewGuid().ToString("N");
            Grant(operation: operation);
            var reduced = new ItemIdentityRegistry(new NamecardServerCatalog
            {
                Products = new[] { new NamecardOffer { Id = "default" } }
            }, new ItemServerCatalog());
            var replay = new SqliteAccountRepository(_path).GrantItem(_account.AccountId, "namecard-galaxy", operation, 1, reduced);
            Assert.AreEqual(ItemGrantResult.Success, replay.Result);
            Assert.IsFalse(replay.IsNewlyApplied);
            Assert.AreEqual(ItemGrantResult.InvalidRequest, Grant("namecard-neon", operation).Result);
            Assert.AreEqual(ItemGrantResult.InvalidRequest, Grant(operation: operation, quantity: 2).Result);
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM item_grant_receipts;"));
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM account_namecards;"));
        }

        [Test]
        public void InvalidRequestsDoNotGrantAnything()
        {
            Assert.AreEqual(ItemGrantResult.InvalidRequest, Grant(operation: "bad-operation").Result);
            Assert.AreEqual(ItemGrantResult.InvalidRequest, Grant(quantity: 0).Result);
            Assert.AreEqual(ItemGrantResult.QuantityLimit, Grant(quantity: 2).Result);
            Assert.AreEqual(ItemGrantResult.UnknownItem, Grant("namecard-missing").Result);
            Assert.AreEqual(ItemGrantResult.UnknownItem, Grant("galaxy").Result);
            Assert.AreEqual(ItemGrantResult.AccountNotFound, _repository.GrantItem("missing-account", "namecard-galaxy",
                Guid.NewGuid().ToString("N"), 1, _registry).Result);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_namecards;"));
        }

        [Test]
        public void SameOperationIdIsScopedToTheRecipient()
        {
            var service = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            var other = service.Register("GrantOther", "password123").Account;
            var operation = Guid.NewGuid().ToString("N");
            Assert.AreEqual(ItemGrantResult.Success, Grant(operation: operation).Result);
            Assert.AreEqual(ItemGrantResult.Success, _repository.GrantItem(other.AccountId, "namecard-galaxy", operation, 1, _registry).Result);
            Assert.AreEqual(2, Sql("SELECT COUNT(*) FROM account_namecards;"));
        }

        [Test]
        public void InventoryGrantsDoNotExecuteContractPurchaseEffectsAndRespectQuantityLimits()
        {
            var ledgerCountBefore = Sql("SELECT COUNT(*) FROM chip_ledger;");
            Assert.AreEqual(ItemGrantResult.Success, Grant("devil-contract").Result);
            Assert.AreEqual(ItemGrantResult.AlreadyOwned, Grant("devil-contract").Result);
            Assert.AreEqual(10000u, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(ItemGrantResult.Success, Grant("token", quantity: 2).Result);
            Assert.AreEqual(ItemGrantResult.QuantityLimit, Grant("token", quantity: 2).Result);
            Assert.AreEqual(ItemGrantResult.Success, Grant("token").Result);
            Assert.AreEqual(3, Sql("SELECT quantity FROM account_items WHERE item_id='token';"));
            Assert.AreEqual(ledgerCountBefore, Sql("SELECT COUNT(*) FROM chip_ledger;"), "Grants must not add chip ledger entries.");
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM item_requests;"));
        }

        [Test]
        public void ServerGrantDoesNotRequireAClientOrChangeRoomAndEquipment()
        {
            _repository.SetLastRoom(_account.AccountId, 1001);
            Assert.AreEqual(ItemGrantResult.Success, Grant().Result);
            var account = _repository.FindById(_account.AccountId);
            Assert.AreEqual(1001, account.LastRoomId);
            Assert.AreEqual("", account.EquippedNamecardId);
            Assert.AreEqual(10000u, account.Chips);
        }

        [Test]
        public void ReceiptFailureRollsBackOwnershipAndVersionAndUsesStorageGuard()
        {
            Sql("CREATE TRIGGER reject_grant BEFORE INSERT ON item_grant_receipts BEGIN SELECT RAISE(ABORT, 'Injected grant receipt failure'); END;");
            bool failed = false;
            var guarded = new GuardedAccountRepository(_repository, _ => failed = true);
            Assert.Throws<SqliteException>(() => guarded.GrantItem(_account.AccountId, "namecard-galaxy",
                Guid.NewGuid().ToString("N"), 1, _registry));
            Assert.IsTrue(failed);
            Assert.IsTrue(guarded.HasFailed);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_namecards;"));
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM item_grant_receipts;"));
            Assert.AreEqual(_account.Version, _repository.FindById(_account.AccountId).Version);
            Sql("DROP TRIGGER reject_grant;");
            Assert.Throws<InvalidOperationException>(() => guarded.GrantItem(_account.AccountId, "namecard-galaxy",
                Guid.NewGuid().ToString("N"), 1, _registry));
        }

        [Test]
        public void SchemaEightUpgradePreservesExistingNamecardAndEquipment()
        {
            _repository.ChangeNamecard(_account.AccountId, Guid.NewGuid().ToString("N"),
                ShopAction.Purchase, "galaxy", _namecards.Revision, _namecards);
            _repository.ChangeNamecard(_account.AccountId, Guid.NewGuid().ToString("N"),
                ShopAction.Equip, "galaxy", _namecards.Revision, _namecards);
            Sql("DROP TABLE item_grant_receipts;");
            Sql("PRAGMA user_version=8;");
            _repository.Initialize();
            _repository.Initialize();
            Assert.AreEqual(SqliteAccountRepository.SchemaVersion, Sql("PRAGMA user_version;"));
            Assert.AreEqual("galaxy", _repository.FindById(_account.AccountId).EquippedNamecardId);
            Assert.AreEqual(4000u, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(ItemGrantResult.AlreadyOwned, Grant().Result);
            Assert.AreEqual(ItemGrantResult.Success, Grant("namecard-neon").Result);
        }
    }
}
