using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Network;
using Game.Network.Accounts;
using Game.UI;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class HandSettlementAndLeaderboardTest
    {
        private string _path;
        private SqliteAccountRepository _repository;
        private static readonly DateTime Created = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerSettlement-" + Guid.NewGuid().ToString("N") + ".db");
            _repository = new SqliteAccountRepository(_path);
            _repository.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private PlayerAccount Create(string id, uint chips = 1000, DateTime? created = null)
        {
            var account = new PlayerAccount
            {
                AccountId = id, UserName = id, Chips = chips, PasswordAlgorithm = "test",
                PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 2 }, PasswordIterations = 1,
                CreatedAtUtc = created ?? Created, UpdatedAtUtc = Created,
            };
            Assert.AreEqual(CreateAccountResult.Created, _repository.Create(account));
            return account;
        }

        private long Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar() ?? 0);
        }

        private HandSettlementEntry[] Begin()
        {
            Create("A"); Create("B"); Create("C");
            Assert.AreEqual(HandStartResult.Started, _repository.BeginHand("hand", 1, new[] { "A", "B", "C" }, 10, out _));
            return new[]
            {
                new HandSettlementEntry { AccountId = "A", Contribution = 300, Award = 400, Refund = 100 },
                new HandSettlementEntry { AccountId = "B", Contribution = 200 },
                new HandSettlementEntry { AccountId = "C" },
            };
        }

        [Test]
        public void SettlementIncludesLoserAndZeroChangeAndReplaysRecordedResult()
        {
            var entries = Begin();
            Assert.IsTrue(_repository.SetLastRoom("A", 3));
            Assert.AreEqual(HandSettlementResult.Applied, _repository.SettleHand("hand", "settle", entries, out var result));
            Assert.AreEqual(1200, result["A"].Chips);
            Assert.AreEqual(800, result["B"].Chips);
            Assert.AreEqual(1000, result["C"].Chips);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(2, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='HandNetSettlement';"));
            Assert.AreEqual(ChipMutationResult.Applied, _repository.ApplyChipMutation("A", "later", "Grant", 100, null, null, out _));
            Assert.AreEqual(HandSettlementResult.AlreadyApplied, _repository.SettleHand("hand", "retry", entries.Reverse().ToArray(), out var replay));
            Assert.AreEqual(1200, replay["A"].Chips);
            Assert.AreEqual(1300, _repository.FindById("A").Chips);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
            entries[0].Award = 300; entries[1].Award = 100;
            Assert.AreEqual(HandSettlementResult.InvalidDistribution, _repository.SettleHand("hand", "conflict", entries, out _));
        }

        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("extra")]
        [TestCase("overdraw")]
        [TestCase("refund")]
        [TestCase("sum")]
        public void InvalidSettlementIsRejectedWithoutPartialWrites(string fault)
        {
            var entries = Begin();
            if (fault == "missing") entries = entries.Take(2).ToArray();
            if (fault == "duplicate") entries[1].AccountId = "A";
            if (fault == "extra") entries[2].AccountId = "Outside";
            if (fault == "overdraw") entries[1].Contribution = 1001;
            if (fault == "refund") entries[1].Refund = 201;
            if (fault == "sum") entries[0].Award++;
            Assert.AreEqual(HandSettlementResult.InvalidDistribution, _repository.SettleHand("hand", "bad", entries, out _));
            Assert.AreEqual(3000, _repository.ReconcileChips().AccountBalanceTotal);
            Assert.AreEqual(3, _repository.ReconcileChips().LedgerEntryCount);
            Assert.AreEqual(1, _repository.ReconcileChips().ActiveHandCount);
        }

        [TestCase("ledger")]
        [TestCase("summary")]
        [TestCase("completion")]
        public void TransactionFailureRollsBackEveryAccountAndFinalState(string point)
        {
            var entries = Begin();
            var target = point == "ledger" ? "INSERT ON chip_ledger" :
                point == "summary" ? "UPDATE ON poker_hand_participants" : "INSERT ON hand_requests";
            Sql("CREATE TRIGGER fail_settle BEFORE " + target + " BEGIN SELECT RAISE(ABORT,'Test failure'); END;");
            Assert.Throws<SqliteException>(() => _repository.SettleHand("hand", "failure", entries, out _));
            foreach (var id in new[] { "A", "B", "C" }) Assert.AreEqual(1000, _repository.FindById(id).Chips);
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM poker_hand_participants WHERE settled_chips IS NOT NULL;"));
            Assert.AreEqual(1, _repository.ReconcileChips().ActiveHandCount);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
            Sql("DROP TRIGGER fail_settle;");
            _repository.Initialize();
            Assert.AreEqual(3, _repository.ReconcileChips().LedgerEntryCount);
            Assert.AreEqual(0, _repository.ReconcileChips().ActiveHandCount);
        }

        [Test]
        public void RestartAfterCommitKeepsResultAndNeverRefundsIt()
        {
            var entries = Begin();
            Assert.AreEqual(HandSettlementResult.Applied, _repository.SettleHand("hand", "commit", entries, out _));
            var before = _repository.ReconcileChips();
            _repository.Initialize(); _repository.Initialize();
            Assert.AreEqual(1200, _repository.FindById("A").Chips);
            Assert.AreEqual(800, _repository.FindById("B").Chips);
            Assert.AreEqual(before.LedgerEntryCount, _repository.ReconcileChips().LedgerEntryCount);
            Assert.IsFalse(_repository.AbortHand("hand", "cleanup"));
        }

        [Test]
        public void MigratesVersionThreeAndRefundsOnlyLegacyActiveContributionsOnce()
        {
            RecreateVersionThree(false);
            _repository.Initialize();
            Assert.AreEqual(SqliteAccountRepository.SchemaVersion, Sql("PRAGMA user_version;"));
            Assert.AreEqual(1000, _repository.FindById("A").Chips);
            Assert.AreEqual(1000, _repository.FindById("B").Chips);
            Assert.IsTrue(_repository.ReconcileChips().IsConsistent);
            Assert.AreEqual(2, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='CrashRecoveryRefund';"));
            _repository.BeginHand("new", 1, new[] { "A", "B" }, 1, out _);
            _repository.Initialize(); _repository.Initialize();
            Assert.AreEqual(2, Sql("SELECT COUNT(*) FROM chip_ledger WHERE transaction_type='CrashRecoveryRefund';"));
            Assert.AreEqual(2000, _repository.ReconcileChips().AccountBalanceTotal);
        }

        [Test]
        public void FailedMigrationDoesNotAdvanceVersionOrPartiallyAddColumns()
        {
            RecreateVersionThree(true);
            Assert.AreEqual(3, Sql("PRAGMA user_version;"), "Fixture schema");
            Assert.AreEqual(1, Sql("SELECT COUNT(*) FROM pragma_table_info('poker_hand_participants') WHERE name='awarded_chips';"), "Fixture conflict");
            Assert.Throws<SqliteException>(() => _repository.Initialize());
            Assert.AreEqual(3, Sql("PRAGMA user_version;"));
            Assert.AreEqual(0, Sql("SELECT COUNT(*) FROM pragma_table_info('poker_hands') WHERE name='accounting_mode';"));
            Assert.AreEqual(1500, Sql("SELECT SUM(chips) FROM player_accounts;"));
        }

        private void RecreateVersionThree(bool conflict)
        {
            SqliteConnection.ClearAllPools(); File.Delete(_path);
            using (var connection = new SqliteConnection("Data Source=" + _path))
            {
                connection.Open(); using var transaction = connection.BeginTransaction();
                foreach (var name in new[] { "CreateSchema", "CreateHandSchema", "CreateSessionSchema" })
                    typeof(SqliteAccountRepository).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { connection, transaction });
                transaction.Commit();
            }
            Sql(@"PRAGMA user_version=3;
INSERT INTO player_accounts VALUES('A','A','test',X'01',X'02',1,700,NULL,0,'2026-09-06T00:00:00.0000000Z','2026-09-06T00:00:00.0000000Z');
INSERT INTO player_accounts VALUES('B','B','test',X'01',X'02',1,800,NULL,0,'2026-09-06T00:00:00.0000000Z','2026-09-06T00:00:00.0000000Z');
INSERT INTO poker_hands VALUES('legacy',1,'Active','2026-09-06T00:00:00.0000000Z',NULL);
INSERT INTO poker_hand_participants VALUES('legacy','A',1000,300),('legacy','B',1000,200);
INSERT INTO chip_ledger VALUES('a0','a0','A',NULL,NULL,'Grant',1000,0,1000,'2026-09-06');
INSERT INTO chip_ledger VALUES('b0','b0','B',NULL,NULL,'Grant',1000,0,1000,'2026-09-06');
INSERT INTO chip_ledger VALUES('a1','a1','A',1,'legacy','HandCommit',-300,1000,700,'2026-09-06');
INSERT INTO chip_ledger VALUES('b1','b1','B',1,'legacy','HandCommit',-200,1000,800,'2026-09-06');");
            if (conflict) Sql("ALTER TABLE poker_hand_participants ADD COLUMN awarded_chips INTEGER;");
        }

        [TestCase(1)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(14)]
        public void LeaderboardUsesStableTiesAndIncludesOfflineSelf(int count)
        {
            for (int i = count - 1; i >= 0; i--) Create("Player" + i.ToString("D2"));
            var id = "Player" + (count - 1).ToString("D2");
            var snapshot = _repository.ReadLeaderboard(id);
            Assert.AreEqual(Math.Min(10, count), snapshot.TopPlayers.Length);
            Assert.AreEqual(count, snapshot.Self.Rank);
            Assert.AreEqual(id, snapshot.Self.UserName);
            for (int i=0; i<snapshot.TopPlayers.Length; i++)
            {
                Assert.AreEqual(i + 1, snapshot.TopPlayers[i].Rank);
                Assert.AreEqual("Player" + i.ToString("D2"), snapshot.TopPlayers[i].UserName);
            }
            Assert.AreEqual(count <= 10 ? 1 : 0, snapshot.TopPlayers.Count(entry => entry.IsSelf));
        }

        [Test]
        public void LeaderboardOrdersBalanceThenRegistrationAndRefreshesAfterSettlementAndRewards()
        {
            var entries = Begin(); Create("Early", 1000, Created.AddDays(-1));
            Assert.AreEqual("Early", _repository.ReadLeaderboard("A").TopPlayers[0].UserName);
            Assert.AreEqual(1000, _repository.ReadLeaderboard("A").Self.Chips);
            _repository.SettleHand("hand", "rank-settle", entries, out _);
            Assert.AreEqual(1, _repository.ReadLeaderboard("A").Self.Rank);
            _repository.ApplyChipMutation("B", "reward", "Grant", 1000, null, null, out _);
            var snapshot = _repository.ReadLeaderboard("A");
            Assert.AreEqual("B", snapshot.TopPlayers[0].UserName);
            Assert.AreEqual(2, snapshot.Self.Rank);
            Assert.AreEqual(1200, snapshot.Self.Chips);
        }

        [Test]
        public void LeaderboardRefreshesEquippedNamecardsForOfflineAndOutsideTopTenPlayers()
        {
            for (int i = 0; i < 12; i++) Create("Player" + i.ToString("D2"));
            Assert.AreEqual(string.Empty, _repository.ReadLeaderboard("Player11").Self.NamecardId);
            Sql("UPDATE player_accounts SET equipped_namecard='galaxy' WHERE account_id='Player00';");
            Sql("UPDATE player_accounts SET equipped_namecard='prism' WHERE account_id='Player11';");
            var snapshot = _repository.ReadLeaderboard("Player11");
            Assert.AreEqual("galaxy", snapshot.TopPlayers[0].NamecardId);
            Assert.AreEqual(12, snapshot.Self.Rank);
            Assert.AreEqual("prism", snapshot.Self.NamecardId);
            Sql("UPDATE player_accounts SET equipped_namecard='neon' WHERE account_id='Player00';");
            Assert.AreEqual("neon", _repository.ReadLeaderboard("Player11").TopPlayers[0].NamecardId);
        }

        [Test]
        public void LeaderboardMessagesRoundTripWithoutAccountSecrets()
        {
            Create("A");
            Sql("UPDATE player_accounts SET equipped_namecard='galaxy' WHERE account_id='A';");
            using var writer = NetworkWriterPool.Get();
            writer.Write(new LeaderboardResponse { RequestId=7, AccountId="A", Result=LeaderboardResult.Success, Snapshot=_repository.ReadLeaderboard("A") });
            var result = new NetworkReader(writer.ToArraySegment()).Read<LeaderboardResponse>();
            Assert.AreEqual(7, result.RequestId);
            Assert.AreEqual(1, result.Snapshot.Self.Rank);
            Assert.IsTrue(result.Snapshot.TopPlayers[0].IsSelf);
            Assert.AreEqual("galaxy", result.Snapshot.TopPlayers[0].NamecardId);
            Assert.AreEqual("galaxy", result.Snapshot.Self.NamecardId);
            CollectionAssert.AreEquivalent(new[] { "Rank", "UserName", "NamecardId", "Chips", "IsSelf" }, typeof(LeaderboardEntry).GetFields().Select(field => field.Name));
        }

        [Test]
        public void LeaderboardRejectsUnauthenticatedReplacedStaleAndFrequentRequests()
        {
            var account=Create("A"); var go=new GameObject("Leaderboard request test");go.SetActive(false);
            try
            {
                var network=go.AddComponent<NetworkRoom>();var auth=go.AddComponent<AccountAuthenticator>();network.authenticator=auth;
                typeof(AccountAuthenticator).GetField("<AccountRepository>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(auth,_repository);
                var players=(PlayerManager)typeof(NetworkRoom).GetField("_playerManager",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(network);
                var conn=new NetworkConnectionToClient(77);players.BindAccount(account,conn);
                var process=typeof(NetworkRoom).GetMethod("ProcessLeaderboardRequest",BindingFlags.Instance|BindingFlags.NonPublic);
                Func<ulong,LeaderboardResponse> request=id=>(LeaderboardResponse)process.Invoke(network,new object[]{conn,new LeaderboardRequest{RequestId=id}});
                Assert.AreEqual(LeaderboardResult.Unavailable,request(1).Result);
                conn.isAuthenticated=true;
                Assert.AreEqual(LeaderboardResult.Success,request(1).Result);
                Assert.AreEqual(LeaderboardResult.InvalidRequest,request(1).Result);
                Assert.AreEqual(LeaderboardResult.RateLimited,request(2).Result);
                players.BindAccount(account,new NetworkConnectionToClient(78){isAuthenticated=true});
                Assert.AreEqual(LeaderboardResult.Unavailable,request(3).Result);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(0u,"0")]
        [TestCase(999u,"999")]
        [TestCase(1000u,"1000")]
        [TestCase(5738u,"5738")]
        [TestCase(500000u,"500000")]
        [TestCase(999999u,"999999")]
        [TestCase(1000000u,"1000K")]
        [TestCase(1000001u,"1000K")]
        [TestCase(1234567u,"1234.6K")]
        [TestCase(1234450u,"1234.4K")]
        [TestCase(1000000000u,"1000000K")]
        [TestCase(uint.MaxValue,"4294967.3K")]
        public void AmountFormattingIsExactBelowMillionAndIndependentOfCulture(uint value,string expected)
        {
            var original=CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture=new CultureInfo("de-DE"); Assert.AreEqual(expected,ChipAmountFormatter.Format(value)); }
            finally { CultureInfo.CurrentCulture=original; }
        }

        [Test]
        public void SupplyAtZeroLeavesComebackClaimableAtExactlyOneThousand()
        {
            Create("A",0);var tasks=new DailyTaskService(_repository,()=>new DateTimeOffset(Created));
            var period=tasks.GetStatus("A").Period;
            Assert.AreEqual(DailyTaskResult.Success,tasks.Claim("A",DailyTaskId.DailySupply,period,out _));
            Assert.AreEqual(DailyTaskAvailability.Claimable,tasks.GetStatus("A").Tasks[1].Availability);
            Assert.AreEqual(DailyTaskResult.Success,tasks.Claim("A",DailyTaskId.Comeback,period,out var account));
            Assert.AreEqual(3000,account.Chips);
            Assert.AreEqual(DailyTaskResult.AlreadyClaimed,tasks.Claim("A",DailyTaskId.Comeback,period,out _));
        }
    }
}
