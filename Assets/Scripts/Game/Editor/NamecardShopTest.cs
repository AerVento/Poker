using System;
using System.IO;
using System.Linq;
using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;
using System.Text.Json;

namespace Game.Tests
{
    public sealed class NamecardShopTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private PlayerAccount _account;
        private NamecardServerCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerShop-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            var service = new AccountService(_repository, new PasswordHasher(iterations: 1000));
            service.Initialize();
            _account = service.Register("ShopUser", "password123").Account;
            _catalog = new NamecardServerCatalog { Products = new[]
            {
                new NamecardOffer { Id = "default", Price = 0 },
                new NamecardOffer { Id = "galaxy", Price = 9000 },
                new NamecardOffer { Id = "neon", Price = 20000 }
            }};
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private ShopResult Change(ShopAction action, string product = "galaxy", string operation = null, string revision = null)
            => _repository.ChangeNamecard(_account.AccountId, operation ?? Guid.NewGuid().ToString("N"),
                action, product, revision ?? _catalog.Revision, _catalog);

        private long Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        [Test]
        public void DefaultOwnedWithoutRowsAndFreeToEquip()
        {
            CollectionAssert.AreEqual(new[] { "default" }, _repository.ReadNamecards(_account.AccountId).OwnedIds);
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Equip, "default"));
            Assert.AreEqual(ShopResult.AlreadyOwned, Change(ShopAction.Purchase, "default"));
            Assert.AreEqual(10000, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_namecards;"));
        }

        [Test]
        public void PurchaseDeductsOnceAndDoesNotEquipUntilRequested()
        {
            var id = Guid.NewGuid().ToString("N");
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Purchase, operation: id));
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Purchase, operation: id));
            Assert.AreEqual(ShopResult.AlreadyOwned, Change(ShopAction.Purchase));
            var snapshot = _repository.ReadNamecards(_account.AccountId);
            Assert.AreEqual(1000, snapshot.Account.Chips);
            Assert.AreEqual("", snapshot.Account.EquippedNamecardId);
            Assert.AreEqual(1, snapshot.Account.Version);
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='ShopPurchase';"));
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Equip));
            var reopened = new SqliteAccountRepository(_path);
            reopened.Initialize();
            Assert.AreEqual("galaxy", reopened.FindById(_account.AccountId).EquippedNamecardId);
            CollectionAssert.Contains(reopened.ReadNamecards(_account.AccountId).OwnedIds, "galaxy");
            Assert.IsTrue(reopened.ReconcileChips().IsConsistent);
        }

        [Test]
        public void OldEquipRetryDoesNotReplaceNewerEquipment()
        {
            Change(ShopAction.Purchase);
            var id = Guid.NewGuid().ToString("N");
            Change(ShopAction.Equip, operation: id);
            Change(ShopAction.Equip, "default");
            Change(ShopAction.Equip, operation: id);
            Assert.AreEqual("", _repository.FindById(_account.AccountId).EquippedNamecardId);
            Assert.AreEqual(ShopResult.InvalidRequest, Change(ShopAction.Equip, "default", id));
        }

        [Test]
        public void InsufficientOrUnownedOrChangedPriceNeverCharges()
        {
            Assert.AreEqual(ShopResult.InsufficientChips, Change(ShopAction.Purchase, "neon"));
            Assert.AreEqual(ShopResult.NotOwned, Change(ShopAction.Equip));
            var revision = _catalog.Revision;
            _catalog.Products[1].Price = 8000;
            Assert.AreEqual(ShopResult.CatalogChanged, Change(ShopAction.Purchase, revision: revision));
            Assert.AreEqual(ShopResult.UnknownProduct, Change(ShopAction.Purchase, "missing"));
            Assert.AreEqual(10000, _repository.FindById(_account.AccountId).Chips);
        }

        [Test]
        public void DelistingRetainsOwnershipAndDefaultSwitch()
        {
            Change(ShopAction.Purchase);
            _catalog.Products[1].Listed = false;
            Assert.AreEqual(ShopResult.UnknownProduct, Change(ShopAction.Purchase));
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Equip));
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Equip, "default"));
        }

        [Test]
        public void RoomAndActiveHandRejectBothChanges()
        {
            _repository.SetLastRoom(_account.AccountId, 1);
            Assert.AreEqual(ShopResult.OutsideRoomRequired, Change(ShopAction.Purchase));
            Assert.AreEqual(ShopResult.OutsideRoomRequired, Change(ShopAction.Equip, "default"));
            _repository.SetLastRoom(_account.AccountId, null);
            var second = new AccountService(_repository, new PasswordHasher(iterations: 1000)).Register("SecondUser", "password123").Account;
            _repository.BeginHand("shop-hand", 1, new[] { _account.AccountId, second.AccountId }, 1, out _);
            Assert.IsFalse(_repository.ReadNamecards(_account.AccountId).CanAct);
            Assert.AreEqual(ShopResult.OutsideRoomRequired, Change(ShopAction.Purchase));
            Assert.AreEqual(ShopResult.OutsideRoomRequired, Change(ShopAction.Equip, "default"));
        }

        [TestCase("account_namecards")]
        [TestCase("chip_ledger")]
        [TestCase("shop_requests")]
        public void FailureRollsBackBalanceInventoryAndReceipt(string table)
        {
            Sql("CREATE TRIGGER shop_fail BEFORE INSERT ON " + table +
                " BEGIN SELECT RAISE(ABORT,'Injected shop failure'); END;");
            int failures = 0;
            var guarded = new GuardedAccountRepository(_repository, _ => failures++);
            Assert.Throws<SqliteException>(() => guarded.ChangeNamecard(_account.AccountId,
                Guid.NewGuid().ToString("N"), ShopAction.Purchase, "galaxy", _catalog.Revision, _catalog));
            Assert.IsTrue(guarded.HasFailed);
            Assert.AreEqual(1, failures);
            Assert.AreEqual(10000, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_namecards;"));
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM shop_requests;"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void EquipFailureKeepsPriorAppearance()
        {
            Change(ShopAction.Purchase);
            Sql("CREATE TRIGGER shop_fail BEFORE INSERT ON shop_requests BEGIN SELECT RAISE(ABORT,'Injected'); END;");
            Assert.Throws<SqliteException>(() => Change(ShopAction.Equip));
            Assert.AreEqual("", _repository.FindById(_account.AccountId).EquippedNamecardId);
        }

        [Test]
        public void PurchaseAllowsComebackAt1000AndLowersLeaderboard()
        {
            Change(ShopAction.Purchase);
            Assert.AreEqual(1000, _repository.ReadLeaderboard(_account.AccountId).Self.Chips);
            Assert.AreEqual(ChipMutationResult.Applied,
                _repository.ClaimDailyReward(_account.AccountId, "shop-daily", 500, 1000, out var account));
            Assert.AreEqual(1500, account.Chips);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void SuccessfulRetrySurvivesCatalogChange()
        {
            var id = Guid.NewGuid().ToString("N");
            var revision = _catalog.Revision;
            Change(ShopAction.Purchase, operation: id);
            _catalog.Products[1].Price = 15000;
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Purchase, operation: id, revision: revision));
            Assert.AreEqual(1000, _repository.FindById(_account.AccountId).Chips);
        }

        [Test]
        public void Schema4MigrationPreservesExistingBalanceAndDefaults()
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_path);
            using (var connection = new SqliteConnection("Data Source=" + _path))
            {
                connection.Open();
                using var transaction = connection.BeginTransaction();
                foreach (var name in new[] { "CreateSchema", "CreateHandSchema", "CreateSessionSchema", "CreateDeferredSettlementSchema" })
                    typeof(SqliteAccountRepository).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                        .Invoke(null, new object[] { connection, transaction });
                transaction.Commit();
            }
            Sql("PRAGMA user_version=4;");
            _repository.Create(_account);
            _repository.Initialize();
            Assert.AreEqual(5, Sql("PRAGMA user_version;"));
            Assert.AreEqual(10000, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual("", _repository.FindById(_account.AccountId).EquippedNamecardId);
            CollectionAssert.AreEqual(new[] { "default" }, _repository.ReadNamecards(_account.AccountId).OwnedIds);
        }

        [Test]
        public void RoomSerializationCarriesOnlyPublicAppearance()
        {
            var room = RoomInfo.CreateRoom("Owner");
            room.Namecards["Owner"] = "galaxy";
            using var writer = NetworkWriterPool.Get();
            writer.WriteRoomInfo(room);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            var copy = reader.ReadRoomInfo();
            Assert.AreEqual("galaxy", copy.GetNamecard("Owner"));
            var json = JsonSerializer.Serialize(copy);
            Assert.IsFalse(json.Contains("OwnedIds"));
            Assert.IsFalse(json.Contains("Password"));
        }

        [Test]
        public void ExactPriceCanSpendBalanceToZero()
        {
            _catalog.Products[1].Price = _account.Chips;
            Assert.AreEqual(ShopResult.Success, Change(ShopAction.Purchase));
            Assert.AreEqual(0, _repository.FindById(_account.AccountId).Chips);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void OperationIdsAreScopedToAccount()
        {
            var second = new AccountService(_repository, new PasswordHasher(iterations: 1000)).Register("SecondUser", "password123").Account;
            var operation = Guid.NewGuid().ToString("N");
            Change(ShopAction.Purchase, operation: operation);
            Assert.AreEqual(ShopResult.Success, _repository.ChangeNamecard(second.AccountId,
                operation, ShopAction.Purchase, "galaxy", _catalog.Revision, _catalog));
            Assert.AreEqual(2, Sql("SELECT COUNT(*) FROM account_namecards;"));
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [Test]
        public void ServerRejectsUnauthenticatedReplacedStaleFrequentAndRoomRequests()
        {
            var go = new UnityEngine.GameObject("Shop request test");
            go.SetActive(false);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                var network = go.AddComponent<NetworkRoom>();
                var auth = go.AddComponent<AccountAuthenticator>();
                network.authenticator = auth;
                typeof(AccountAuthenticator).GetField("<AccountRepository>k__BackingField", flags).SetValue(auth, _repository);
                typeof(NetworkRoom).GetField("_shopCatalog", flags).SetValue(network, _catalog);
                var players = (PlayerManager)typeof(NetworkRoom).GetField("_playerManager", flags).GetValue(network);
                var conn = new NetworkConnectionToClient(77);
                players.BindAccount(_account, conn);
                var method = typeof(NetworkRoom).GetMethod("ProcessShopRequest", flags);
                Func<ulong, NamecardShopResponse> request = id => (NamecardShopResponse)method.Invoke(network,
                    new object[] { conn, new NamecardShopRequest { RequestId = id, Action = ShopAction.Read } });
                Assert.AreEqual(ShopResult.Unavailable, request(1).Result);
                conn.isAuthenticated = true;
                Assert.AreEqual(ShopResult.Success, request(1).Result);
                Assert.AreEqual(ShopResult.InvalidRequest, request(1).Result);
                Assert.AreEqual(ShopResult.RateLimited, request(2).Result);
                var replacement = new NetworkConnectionToClient(78) { isAuthenticated = true };
                _account.LastRoomId = 3;
                players.BindAccount(_account, replacement);
                Assert.AreEqual(ShopResult.Unavailable, request(3).Result);
                conn = replacement;
                Assert.AreEqual(ShopResult.OutsideRoomRequired, request(1).Result);
                Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM account_namecards;"));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void CatalogRejectsDuplicateAndInvalidIds()
        {
            _catalog.Validate();
            _catalog.Products[1].Id = "default";
            Assert.Throws<InvalidOperationException>(() => _catalog.Validate());
            Assert.IsFalse(NamecardServerCatalog.IsValidId("../bad"));
        }
    }
}
