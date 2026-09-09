using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Tests
{
    public partial class DailyTaskTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private DailyTaskService _tasks;
        private DateTimeOffset _now;
        private PlayerAccount _account;
        private const int Period = 20260905;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerTasks-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            _repository.Initialize();
            _now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
            _tasks = new DailyTaskService(_repository, () => _now);
            _account = Create("Alice", 900);
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        [TestCase(0u, DailyTaskAvailability.Claimable)]
        [TestCase(999u, DailyTaskAvailability.Claimable)]
        [TestCase(1000u, DailyTaskAvailability.Claimable)]
        [TestCase(1001u, DailyTaskAvailability.BalanceTooHigh)]
        public void LowBalanceThresholdIsStrict(uint balance, DailyTaskAvailability expected)
        {
            ChangeBalance(balance);
            var status = _tasks.GetStatus(_account.AccountId);
            Assert.AreEqual(balance, status.Balance);
            Assert.AreEqual(2, status.Tasks.Length);
            Assert.AreEqual(expected, status.Tasks[1].Availability);
            var result = Claim(DailyTaskId.Comeback);
            Assert.AreEqual(expected == DailyTaskAvailability.Claimable ? DailyTaskResult.Success : DailyTaskResult.BalanceTooHigh, result);
        }

        [Test]
        public void BothRewardsHaveSeparateQuotasAndPersistAcrossRestart()
        {
            Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.Comeback));
            Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.DailySupply));
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed, Claim(DailyTaskId.Comeback));
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed, Claim(DailyTaskId.DailySupply));
            var reopened = new SqliteAccountRepository(_path);
            reopened.Initialize();
            var service = new DailyTaskService(reopened, () => _now);
            var state = service.GetStatus(_account.AccountId);
            Assert.AreEqual(3900, state.Balance);
            Assert.IsTrue(state.Tasks.All(task => task.Availability == DailyTaskAvailability.Claimed));
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed, service.Claim(_account.AccountId, DailyTaskId.DailySupply, Period, out _));
            Assert.IsTrue(reopened.ReconcileChips().IsConsistent);
        }

        [Test]
        public void ClaimRechecksBalanceInsteadOfTrustingOpenedPanel()
        {
            Assert.AreEqual(DailyTaskAvailability.Claimable, _tasks.GetStatus(_account.AccountId).Tasks[1].Availability);
            Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.DailySupply));
            Assert.AreEqual(DailyTaskResult.BalanceTooHigh, Claim(DailyTaskId.Comeback));
            Assert.AreEqual(1900, _repository.FindById(_account.AccountId).Chips);
            ChangeBalance(999);
            Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.Comeback));
            Assert.AreEqual(2999, _repository.FindById(_account.AccountId).Chips);
        }

        [Test]
        public void UtcPlusEightMidnightResetsBothQuotasAndRejectsOldPackets()
        {
            _now = new DateTimeOffset(2026, 9, 5, 15, 59, 59, TimeSpan.Zero);
            Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.Comeback));
            Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.DailySupply));
            var before = _tasks.GetStatus(_account.AccountId);
            Assert.AreEqual(Period, before.Period);
            Assert.AreEqual(1, before.NextResetUnixSeconds - before.ServerUnixSeconds);
            _now = _now.AddSeconds(1);
            ChangeBalance(900);
            var after = _tasks.GetStatus(_account.AccountId);
            Assert.AreEqual(20260906, after.Period);
            Assert.AreEqual(86400, after.NextResetUnixSeconds - after.ServerUnixSeconds);
            Assert.IsTrue(after.Tasks.All(task => task.Availability == DailyTaskAvailability.Claimable));
            Assert.AreEqual(DailyTaskResult.PeriodChanged, Claim(DailyTaskId.DailySupply));
            Assert.AreEqual(900, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(DailyTaskResult.Success, _tasks.Claim(_account.AccountId, DailyTaskId.DailySupply, after.Period, out _));
        }

        [TestCase(-10)]
        [TestCase(0)]
        [TestCase(8)]
        [TestCase(14)]
        public void ResetUsesFixedTimezoneRegardlessOfClockOffset(int offsetHours)
        {
            _now = new DateTimeOffset(2026, 12, 31, 16, 0, 0, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(offsetHours));
            var state = _tasks.GetStatus(_account.AccountId);
            Assert.AreEqual(20270101, state.Period);
            Assert.AreEqual(new DateTimeOffset(2027, 1, 1, 16, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), state.NextResetUnixSeconds);
        }

        [TestCase(0, 20260905, DailyTaskResult.InvalidRequest)]
        [TestCase(255, 20260905, DailyTaskResult.InvalidRequest)]
        [TestCase(1, 20260904, DailyTaskResult.PeriodChanged)]
        [TestCase(2, 20260906, DailyTaskResult.PeriodChanged)]
        public void InvalidTaskOrPeriodCannotMintChips(int id, int period, DailyTaskResult expected)
        {
            Assert.AreEqual(expected, _tasks.Claim(_account.AccountId, (DailyTaskId)id, period, out _));
            Assert.AreEqual(900, _repository.FindById(_account.AccountId).Chips);
            Assert.AreEqual(1, _repository.ReconcileChips().LedgerEntryCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActiveHandRejectsClaimsAndNeverQueuesPayment(bool previouslyClaimed)
        {
            if (previouslyClaimed) Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.DailySupply));
            Create("Bob", 900);
            Assert.AreEqual(HandStartResult.Started, _repository.BeginHand("hand", 5, new[] { "Alice", "Bob" }, 100, out _));
            var balance = _repository.FindById("Alice").Chips;
            var entries = _repository.ReconcileChips().LedgerEntryCount;
            Assert.IsTrue(_tasks.GetStatus("Alice").Tasks.All(task => task.Availability == DailyTaskAvailability.ActiveHand));
            Assert.AreEqual(DailyTaskResult.ActiveHand, Claim(DailyTaskId.DailySupply));
            Assert.AreEqual(DailyTaskResult.ActiveHand, Claim(DailyTaskId.Comeback));
            Assert.AreEqual(entries, _repository.ReconcileChips().LedgerEntryCount);
            Assert.IsTrue(_repository.AbortHand("hand", "test ended"));
            Assert.AreEqual(balance, _repository.FindById("Alice").Chips);
            Assert.AreEqual(previouslyClaimed ? DailyTaskResult.AlreadyClaimed : DailyTaskResult.Success, Claim(DailyTaskId.DailySupply));
        }

        [Test]
        public void ConcurrentClaimsAcrossRepositoriesCreditExactlyOnce()
        {
            var other = new DailyTaskService(new SqliteAccountRepository(_path), () => _now);
            var claims = new[]
            {
                Task.Run(() => Claim(DailyTaskId.Comeback)),
                Task.Run(() => other.Claim("Alice", DailyTaskId.Comeback, Period, out _)),
            };
            Task.WaitAll(claims);
            CollectionAssert.AreEquivalent(new[] { DailyTaskResult.Success, DailyTaskResult.AlreadyClaimed }, claims.Select(task => task.Result));
            Assert.AreEqual(2900, _repository.FindById("Alice").Chips);
            Assert.AreEqual(2, _repository.ReconcileChips().LedgerEntryCount);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
        }

        [TestCase("player_accounts", "UPDATE OF chips")]
        [TestCase("chip_ledger", "INSERT")]
        public void StorageFailureRollsBackBalanceAndQuotaAndTriggersFatalGuard(string table, string operation)
        {
            var failures = 0;
            var guarded = new GuardedAccountRepository(_repository, _ => failures++);
            var service = new DailyTaskService(guarded, () => _now);
            Sql($"CREATE TRIGGER fail_reward BEFORE {operation} ON {table} BEGIN SELECT RAISE(ABORT, 'Injected reward failure'); END;");
            Assert.Throws<SqliteException>(() => service.Claim("Alice", DailyTaskId.DailySupply, Period, out _));
            Assert.IsTrue(guarded.HasFailed);
            Assert.AreEqual(1, failures);
            Assert.AreEqual(900, _repository.FindById("Alice").Chips);
            Assert.AreEqual(DailyTaskAvailability.Claimable, _tasks.GetStatus("Alice").Tasks[0].Availability);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
            Assert.Throws<InvalidOperationException>(() => service.GetStatus("Alice"));
            Assert.AreEqual(1, failures);
            Sql("DROP TRIGGER fail_reward;");
            Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.DailySupply));
        }

        [Test]
        public void OverflowDoesNotConsumeDailyQuota()
        {
            ChangeBalance(uint.MaxValue - 999);
            Assert.AreEqual(DailyTaskAvailability.BalanceLimit, _tasks.GetStatus("Alice").Tasks[0].Availability);
            Assert.AreEqual(DailyTaskResult.BalanceLimit, Claim(DailyTaskId.DailySupply));
            ChangeBalance(uint.MaxValue - 1000);
            Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.DailySupply));
            Assert.AreEqual(uint.MaxValue, _repository.FindById("Alice").Chips);
        }

        [Test]
        public void AccountsHaveIndependentDailyQuotas()
        {
            Create("Bob", 900);
            Assert.AreEqual(DailyTaskResult.Success, Claim(DailyTaskId.DailySupply));
            Assert.AreEqual(DailyTaskResult.Success, _tasks.Claim("Bob", DailyTaskId.DailySupply, Period, out _));
            Assert.AreEqual(1900, _repository.FindById("Bob").Chips);
        }

        [TestCase("waiting")]
        [TestCase("spectating")]
        [TestCase("last-room")]
        [TestCase("unauthenticated")]
        [TestCase("replaced")]
        public void ServerRejectsRoomMembersAndConnectionsWithoutAccountOwnership(string situation)
        {
            var go = new GameObject("Daily tasks server test");
            go.SetActive(false);
            try
            {
                var network = go.AddComponent<NetworkRoom>();
                Set(network, "_dailyTaskService", _tasks);
                var players = Field<PlayerManager>(network, "_playerManager");
                var rooms = Field<RoomManager>(network, "_roomManager");
                var connection = new NetworkConnectionToClient(71) { isAuthenticated = true };
                players.BindAccount(_account, connection);
                var expected = DailyTaskResult.OutsideRoomRequired;
                if (situation == "waiting") rooms.CreateRoom("Alice");
                if (situation == "spectating")
                {
                    var room = rooms.CreateRoom("Bob");
                    room.IsWaiting = false;
                    Assert.AreEqual(0, rooms.Join(room.Id, "Alice"));
                }
                if (situation == "last-room") players.SetLastRoom("Alice", 5);
                if (situation == "unauthenticated")
                {
                    connection.isAuthenticated = false;
                    expected = DailyTaskResult.Unavailable;
                }
                if (situation == "replaced")
                {
                    players.BindAccount(_account, new NetworkConnectionToClient(72) { isAuthenticated = true });
                    expected = DailyTaskResult.Unavailable;
                }
                var query = (DailyTasksResponse)Invoke(network, "ProcessDailyTasksRequest", connection, new DailyTasksRequest { RequestId = 1 });
                Assert.AreEqual(expected == DailyTaskResult.Unavailable ? expected : DailyTaskResult.Success, query.Result);
                if (expected == DailyTaskResult.Unavailable) Assert.IsNull(query.Snapshot.Tasks);
                else Assert.IsNotNull(query.Snapshot.Tasks);
                var response = ServerClaim(network, connection, 2);
                Assert.AreEqual(expected, response.Result);
                Assert.AreEqual(0, response.GrantedAmount);
                var deletion = (DailyTaskDeleteResponse)Invoke(network, "ProcessDailyTaskDelete", connection,
                    new DailyTaskDeleteRequest { RequestId = 3, InstanceId = Guid.NewGuid().ToString("N") }, null);
                Assert.AreEqual(expected, deletion.Result);
                Assert.AreEqual(0, deletion.GrantedAmount);
                Assert.AreEqual(900, _repository.FindById("Alice").Chips);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ServerRejectsOutOfOrderRequestAndAcceptsFreshRetryWithoutDuplicateCredit()
        {
            var go = new GameObject("Daily tasks sequence test");
            go.SetActive(false);
            try
            {
                var network = go.AddComponent<NetworkRoom>();
                Set(network, "_dailyTaskService", _tasks);
                var connection = new NetworkConnectionToClient(71) { isAuthenticated = true };
                Field<PlayerManager>(network, "_playerManager").BindAccount(_account, connection);
                Invoke(network, "ProcessDailyTasksRequest", connection, new DailyTasksRequest { RequestId = 5 });
                Assert.AreEqual(DailyTaskResult.InvalidRequest, ServerClaim(network, connection, 4).Result);
                Assert.AreEqual(DailyTaskResult.Success, ServerClaim(network, connection, 6).Result);
                Assert.AreEqual(DailyTaskResult.InvalidRequest, ServerClaim(network, connection, 6).Result);
                Assert.AreEqual(DailyTaskResult.AlreadyClaimed, ServerClaim(network, connection, 7).Result);
                Assert.AreEqual(1900, _repository.FindById("Alice").Chips);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void MirrorRoundTripPreservesTaskSnapshotAndRequestCorrelation()
        {
            var deletion = RoundTrip(new DailyTaskDeleteRequest { RequestId = 123, InstanceId = "instance" });
            Assert.AreEqual(123, deletion.RequestId);
            Assert.AreEqual("instance", deletion.InstanceId);
            var deleted = RoundTrip(new DailyTaskDeleteResponse { RequestId = 123, AccountId = "Alice", InstanceId = "instance",
                Result = DailyTaskResult.Success, GrantedAmount = 100, GrantedItems = new[] { new TaskRewardItemState { ItemId = "item", Count = 2 } } });
            Assert.AreEqual(100, deleted.GrantedAmount);
            Assert.AreEqual("Alice", deleted.AccountId);
            Assert.AreEqual("instance", deleted.InstanceId);
            Assert.AreEqual(2, deleted.GrantedItems.Single().Count);
            var state = RoundTrip(new DailyTasksResponse { Snapshot = new DailyTasksSnapshot { Tasks = new[] { new DailyTaskState { CanDelete = true } } } });
            Assert.IsTrue(state.Snapshot.Tasks.Single().CanDelete);
            var response = RoundTrip(new DailyTasksResponse { RequestId = 99, AccountId = "Alice", Result = DailyTaskResult.Success, Snapshot = _tasks.GetStatus("Alice") });
            Assert.AreEqual(99, response.RequestId);
            Assert.AreEqual("Alice", response.AccountId);
            Assert.AreEqual(Period, response.Snapshot.Period);
            Assert.AreEqual(900, response.Snapshot.Balance);
            Assert.AreEqual(2, response.Snapshot.Tasks.Length);
            Assert.AreEqual("东山再起", response.Snapshot.Tasks[1].Name);
            Assert.AreEqual(2000, response.Snapshot.Tasks[1].Reward);
            Assert.AreEqual(DailyTaskAvailability.Claimable, response.Snapshot.Tasks[1].Availability);
            Assert.AreEqual(87, RoundTrip(new DailyTasksRequest { RequestId = 87 }).RequestId);
            var claim = RoundTrip(new DailyTaskClaimRequest { RequestId = 100, TaskId = DailyTaskId.Comeback, Period = Period });
            Assert.AreEqual(DailyTaskId.Comeback, claim.TaskId);
            Assert.AreEqual(Period, claim.Period);
            Assert.AreEqual(100, claim.RequestId);
            var result = RoundTrip(new DailyTaskClaimResponse { RequestId = 100, AccountId = "Alice", TaskId = DailyTaskId.Comeback, Result = DailyTaskResult.Success, GrantedAmount = 2000 });
            Assert.AreEqual(2000, result.GrantedAmount);
            Assert.AreEqual(DailyTaskResult.Success, result.Result);
        }

        private PlayerAccount Create(string name, uint chips)
        {
            var account = new PlayerAccount
            {
                AccountId = name, UserName = name, Chips = chips,
                PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 1 },
                PasswordIterations = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            };
            Assert.AreEqual(CreateAccountResult.Created, _repository.Create(account));
            return account;
        }

        private DailyTaskResult Claim(DailyTaskId id) => _tasks.Claim(_account.AccountId, id, Period, out _);

        private void ChangeBalance(uint balance)
        {
            var before = _repository.FindById(_account.AccountId).Chips;
            Assert.AreEqual(ChipMutationResult.Applied, _repository.ApplyChipMutation(_account.AccountId,
                Guid.NewGuid().ToString("N"), "TestAdjustment", (long)balance - before, null, null, out _));
        }

        private void Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static T RoundTrip<T>(T message) where T : struct, NetworkMessage
        {
            using var writer = NetworkWriterPool.Get();
            writer.Write(message);
            using var reader = NetworkReaderPool.Get(writer.ToArraySegment());
            return reader.Read<T>();
        }

        private static DailyTaskClaimResponse ServerClaim(NetworkRoom network, NetworkConnectionToClient connection, ulong sequence)
            => (DailyTaskClaimResponse)Invoke(network, "ProcessDailyTaskClaim", connection,
                new DailyTaskClaimRequest { RequestId = sequence, TaskId = DailyTaskId.DailySupply, Period = Period }, null);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }
}
