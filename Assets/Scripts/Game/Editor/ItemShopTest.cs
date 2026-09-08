using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ItemShopTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private PlayerAccount _account;
        private ItemServerCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerItems-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            var service = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            service.Initialize();
            _account = service.Register("ItemUser", "password123").Account;
            _catalog = new ItemServerCatalog { Products = new[]
            {
                new ServerItemDefinition { Id = "keepsake", Kind = ItemKind.NonConsumable, Price = 300, MaxQuantity = 1 },
                new ServerItemDefinition { Id = "future", Kind = ItemKind.Consumable, Price = 100, MaxQuantity = 2 }
            }};
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private ItemShopResult Change(ItemShopAction action = ItemShopAction.Purchase, string item = "keepsake",
            string operation = null, string revision = null, uint? price = null, uint quantity = 1)
            => _repository.ChangeItem(_account.AccountId, operation ?? Guid.NewGuid().ToString("N"), action,
                item, quantity, price ?? (action == ItemShopAction.Purchase ? _catalog.Products.FirstOrDefault(p => p.Id == item)?.Price ?? 0 : 0),
                revision ?? Runtime().Revision, Runtime()).Result;

        private ItemPurchaseRuntime Runtime()
        {
            var registry = new ItemPurchaseRegistry();
            foreach (var product in _catalog.Products) registry.Register(product.Id, new ItemPurchasePolicy());
            return new ItemPurchaseRuntime(_catalog, registry);
        }

        private long Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        [Test]
        public void PermanentPurchaseIsAtomicIdempotentAndPersistsAfterRestart()
        {
            var operation = Guid.NewGuid().ToString("N");
            Assert.AreEqual(ItemShopResult.Success, Change(operation: operation));
            Assert.AreEqual(ItemShopResult.Success, Change(operation: operation));
            Assert.AreEqual(ItemShopResult.AlreadyOwned, Change());
            var reopened = new SqliteAccountRepository(_path);
            reopened.Initialize();
            var snapshot = reopened.ReadItems(_account.AccountId);
            Assert.AreEqual(9700, snapshot.Account.Chips);
            Assert.AreEqual(1, snapshot.Account.Version);
            Assert.AreEqual(1, snapshot.Items.Single().Quantity);
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='ItemPurchase';"));
            Assert.IsTrue(reopened.ReconcileChips().IsConsistent);
        }

        [Test]
        public void ReceiptSurvivesCatalogChangeButRejectsDifferentPayload()
        {
            var operation = Guid.NewGuid().ToString("N");
            var revision = Runtime().Revision;
            Change(operation: operation);
            _catalog.Products[0].Price = 400;
            Assert.AreEqual(ItemShopResult.Success, Change(operation: operation, revision: revision, price: 300));
            Assert.AreEqual(ItemShopResult.InvalidRequest, Change(operation: operation));
            Assert.AreEqual(ItemShopResult.InvalidRequest, Change(ItemShopAction.Use, operation: operation, revision: revision));
            Assert.AreEqual(9700, _repository.FindById(_account.AccountId).Chips);
        }

        [Test]
        public void AccountsHavePrivateInventoryAndSeparateOperationIds()
        {
            var other = new AccountService(_repository, new PasswordHasher(iterations: 1000)).Register("OtherItemUser", "password123").Account;
            var operation = Guid.NewGuid().ToString("N");
            Change(operation: operation);
            Assert.IsEmpty(_repository.ReadItems(other.AccountId).Items);
            Assert.AreEqual(ItemShopResult.Success, _repository.ChangeItem(other.AccountId, operation,
                ItemShopAction.Purchase, "keepsake", 1, 300, Runtime().Revision, Runtime()).Result);
            Assert.AreEqual(2, Sql("SELECT COUNT(*) FROM account_items;"));
        }

        [Test]
        public void RejectedRequestsNeverChargeOrCreateInventory()
        {
            Assert.AreEqual(ItemShopResult.InvalidRequest, Change(quantity: 0));
            Assert.AreEqual(ItemShopResult.InvalidRequest, Change(quantity: uint.MaxValue));
            Assert.AreEqual(ItemShopResult.InvalidRequest, Change(operation: "bad"));
            Assert.AreEqual(ItemShopResult.InvalidRequest, Change(item: "../invalid"));
            Assert.AreEqual(ItemShopResult.InvalidRequest, Change((ItemShopAction)255));
            Assert.AreEqual(ItemShopResult.CatalogChanged, Change(price: 0));
            Assert.AreEqual(ItemShopResult.CatalogChanged, Change(revision: "old"));
            Assert.AreEqual(ItemShopResult.UnknownItem, Change(item: "missing"));
            _catalog.Products[0].Price = 10001;
            Assert.AreEqual(ItemShopResult.InsufficientChips, Change());
            Assert.AreEqual(10000, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_items;"));
        }

        [TestCase(0u)]
        [TestCase(10000u)]
        public void FreeAndExactBalancePurchasesReconcile(uint price)
        {
            _catalog.Products[0].Price = price;
            Assert.AreEqual(ItemShopResult.Success, Change());
            Assert.AreEqual(10000 - price, _repository.FindById(_account.AccountId).Chips);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void DelistedAndRemovedItemsRemainInInventory()
        {
            Change();
            _catalog.Products[0].Listed = false;
            Assert.AreEqual(ItemShopResult.UnknownItem, Change());
            _catalog.Products = Array.Empty<ServerItemDefinition>();
            Assert.AreEqual("keepsake", _repository.ReadItems(_account.AccountId).Items.Single().ItemId);
            Assert.AreEqual(ItemShopResult.UnknownItem, Change(ItemShopAction.Use));
        }

        [Test]
        public void ConsumableUseIsReservedAndNeverConsumesOrCredits()
        {
            Assert.AreEqual(ItemShopResult.NotOwned, Change(ItemShopAction.Use, "future"));
            Change();
            Assert.AreEqual(ItemShopResult.NotConsumable, Change(ItemShopAction.Use));
            Assert.AreEqual(ItemShopResult.Success, Change(item: "future"));
            Assert.AreEqual(ItemShopResult.Success, Change(item: "future"));
            Assert.AreEqual(ItemShopResult.QuantityLimit, Change(item: "future"));
            var operation = Guid.NewGuid().ToString("N");
            Assert.AreEqual(ItemShopResult.EffectUnavailable, Change(ItemShopAction.Use, "future", operation));
            Assert.AreEqual(ItemShopResult.EffectUnavailable, Change(ItemShopAction.Use, "future", operation));
            var snapshot = _repository.ReadItems(_account.AccountId);
            Assert.AreEqual(2, snapshot.Items.Single(p => p.ItemId == "future").Quantity);
            Assert.AreEqual(9500, snapshot.Account.Chips);
        }

        [Test]
        public void RoomAndActiveHandBlockSpending()
        {
            _repository.SetLastRoom(_account.AccountId, 1);
            Assert.AreEqual(ItemShopResult.OutsideRoomRequired, Change());
            Assert.IsFalse(_repository.ReadItems(_account.AccountId).CanAct);
            _repository.SetLastRoom(_account.AccountId, null);
            var other = new AccountService(_repository, new PasswordHasher(iterations: 1000)).Register("HandItemUser", "password123").Account;
            _repository.BeginHand("item-hand", 1, new[] { _account.AccountId, other.AccountId }, 1, out _);
            Assert.IsFalse(_repository.ReadItems(_account.AccountId).CanAct);
            Assert.AreEqual(ItemShopResult.OutsideRoomRequired, Change());
            Assert.AreEqual(ItemShopResult.OutsideRoomRequired, Change(ItemShopAction.Use));
        }

        [TestCase("account_items")]
        [TestCase("chip_ledger")]
        [TestCase("item_requests")]
        public void StorageFailureRollsBackEverythingAndTriggersGuard(string table)
        {
            Sql("CREATE TRIGGER item_fail BEFORE INSERT ON " + table + " BEGIN SELECT RAISE(ABORT,'Injected'); END;");
            var failures = 0;
            var guarded = new GuardedAccountRepository(_repository, _ => failures++);
            Assert.Throws<SqliteException>(() => guarded.ChangeItem(_account.AccountId, Guid.NewGuid().ToString("N"),
                ItemShopAction.Purchase, "keepsake", 1, 300, Runtime().Revision, Runtime()));
            Assert.IsTrue(guarded.HasFailed);
            Assert.AreEqual(1, failures);
            Assert.AreEqual(10000, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(0, _repository.FindById(_account.AccountId).Version);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_items;"));
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM item_requests;"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void Schema5MigrationPreservesAccountsAndNamecards()
        {
            var namecards = new NamecardServerCatalog { Products = new[] {
                new NamecardOffer { Id = "default" }, new NamecardOffer { Id = "keepsake", Price = 100 } } };
            _repository.ChangeNamecard(_account.AccountId, Guid.NewGuid().ToString("N"), ShopAction.Purchase,
                "keepsake", namecards.Revision, namecards);
            Sql("DROP TABLE item_requests;");
            Sql("DROP TABLE account_items;");
            Sql("PRAGMA user_version=5;");
            _repository.Initialize();
            Assert.AreEqual(SqliteAccountRepository.SchemaVersion, Sql("PRAGMA user_version;"));
            Assert.AreEqual(9900, _repository.FindById(_account.AccountId).Chips);
            CollectionAssert.Contains(_repository.ReadNamecards(_account.AccountId).OwnedIds, "keepsake");
            Assert.IsEmpty(_repository.ReadItems(_account.AccountId).Items);
            Assert.AreEqual(ItemShopResult.Success, Change());
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void MirrorGeneratedSerializersRoundTripRequestAndPrivateSnapshot()
        {
            var request = new ItemShopRequest { RequestId = 27, Action = ItemShopAction.Purchase,
                ItemId = "keepsake", OperationId = Guid.NewGuid().ToString("N"), Quantity = 1,
                QuotedPrice = 300, CatalogRevision = Runtime().Revision };
            using var writer = NetworkWriterPool.Get();
            writer.Write(request);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            var copy = reader.Read<ItemShopRequest>();
            Assert.AreEqual(request.OperationId, copy.OperationId);
            Assert.AreEqual(request.CatalogRevision, copy.CatalogRevision);
            Assert.AreEqual(300, copy.QuotedPrice);
            Assert.AreEqual(1, copy.Quantity);
            writer.Reset();
            writer.Write(new ItemShopResponse { RequestId = 27, OperationId = request.OperationId,
                AccountId = _account.AccountId, HasSnapshot = true, Chips = 9700, CanAct = true,
                Items = new[] { new ItemStackMessage { ItemId = "keepsake", Quantity = 1 } },
                Offers = new[] { new ItemOfferMessage { ItemId = "keepsake", Price = 300, Listed = true } } });
            using var responseReader = NetworkReaderPool.Get(writer.ToArraySegment());
            var response = responseReader.Read<ItemShopResponse>();
            Assert.AreEqual(_account.AccountId, response.AccountId);
            Assert.AreEqual(1, response.Items.Single().Quantity);
            Assert.AreEqual(300, response.Offers.Single().Price);
            Assert.IsTrue(response.HasSnapshot && response.CanAct);
        }

        [Test]
        public void ServerRejectsUnauthenticatedReplacedStaleFrequentAndInRoomWrites()
        {
            var go = new UnityEngine.GameObject("Item request test");
            go.SetActive(false);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var network = go.AddComponent<NetworkRoom>();
                var auth = go.AddComponent<AccountAuthenticator>();
                network.authenticator = auth;
                typeof(AccountAuthenticator).GetField("<AccountRepository>k__BackingField", flags).SetValue(auth, _repository);
                typeof(NetworkRoom).GetField("_itemRuntime", flags).SetValue(network, Runtime());
                var players = (PlayerManager)typeof(NetworkRoom).GetField("_playerManager", flags).GetValue(network);
                var conn = new NetworkConnectionToClient(71);
                players.BindAccount(_account, conn);
                var method = typeof(NetworkRoom).GetMethod("ProcessItemRequest", flags);
                Func<ulong, ItemShopAction, ItemShopResponse> request = (id, action) => (ItemShopResponse)method.Invoke(network,
                    new object[] { conn, new ItemShopRequest { RequestId = id, Action = action,
                        ItemId = "keepsake", OperationId = Guid.NewGuid().ToString("N"), Quantity = 1,
                        QuotedPrice = 300, CatalogRevision = Runtime().Revision } });
                Assert.AreEqual(ItemShopResult.Unavailable, request(1, ItemShopAction.Read).Result);
                conn.isAuthenticated = true;
                Assert.IsTrue(request(1, ItemShopAction.Read).HasSnapshot);
                Assert.AreEqual(ItemShopResult.InvalidRequest, request(1, ItemShopAction.Purchase).Result);
                Assert.AreEqual(ItemShopResult.RateLimited, request(2, ItemShopAction.Purchase).Result);
                var replacement = new NetworkConnectionToClient(72) { isAuthenticated = true };
                _account.LastRoomId = 3;
                players.BindAccount(_account, replacement);
                Assert.AreEqual(ItemShopResult.Unavailable, request(3, ItemShopAction.Purchase).Result);
                conn = replacement;
                var rejection = request(1, ItemShopAction.Purchase);
                Assert.AreEqual(ItemShopResult.OutsideRoomRequired, rejection.Result);
                Assert.IsTrue(rejection.HasSnapshot);
                Assert.IsFalse(rejection.CanAct);
                Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_items;"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void CatalogValidatesPermanentLimitsAndRejectsDuplicates()
        {
            new ItemServerCatalog().Validate();
            _catalog.Validate();
            _catalog.Products[0].MaxQuantity = 2;
            Assert.Throws<InvalidOperationException>(() => _catalog.Validate());
            _catalog.Products[0].MaxQuantity = 1;
            _catalog.Products[1].Id = "keepsake";
            Assert.Throws<InvalidOperationException>(() => _catalog.Validate());
        }
    }
}
