using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Game.Tests
{
    [TestFixture]
    public class FatalStorageTest
    {
        private string _path;
        private SqliteAccountRepository _raw;
        private GuardedAccountRepository _guard;
        private GameManager _games;
        private int _failures;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "PokerFatal-" + Guid.NewGuid().ToString("N") + ".db");
            _raw = new SqliteAccountRepository(_path);
            _guard = new GuardedAccountRepository(_raw, _ =>
            {
                _failures++;
                _games?.StopForStorageFailure();
            });
            _guard.Initialize();
            _games = new GameManager(_guard);
            _failures = 0;
            foreach (var name in new[] { "A", "B", "C", "D" })
            {
                _guard.Create(new PlayerAccount
                {
                    AccountId = name, UserName = name, Chips = 1000,
                    PasswordAlgorithm = "test", PasswordHash = new byte[] { 1 }, PasswordSalt = new byte[] { 1 },
                    PasswordIterations = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
                });
            }
        }

        [TearDown]
        public void TearDown()
        {
            _games?.StopForStorageFailure();
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
        }

        private void Sql(string sql)
        {
            using var connection = new SqliteConnection("Data Source=" + _path);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private ServerGameInfo Start(int roomId = 42, string first = "A", string second = "B")
        {
            var room = new RoomInfo
            {
                Id = roomId, SmallBlindChips = 50, ThinkingTime = 1000, IsWaiting = false,
                Players = new List<string> { first, second }, ReadyStatus = new List<bool> { true, true },
            };
            Assert.AreEqual(0, _games.CreateGame(room));
            Assert.AreEqual(0, _games.StartNew(roomId));
            return _games.GetGameInfo(roomId);
        }

        [TestCase("allin")]
        [TestCase("showdown")]
        [TestCase("fold")]
        public void SettlementFailure_StopsEveryTableWithoutRetryOrRefund(string route)
        {
            var info = Start();
            var other = Start(43, "C", "D");
            int completed = 0;
            _games.OnRoomRoundEnded += (_, __) => completed++;
            if (route == "allin")
            {
                Assert.AreEqual(0, _games.AllIn(42, info.OperatingPlayer));
                Assert.AreEqual(0, _games.AllIn(42, info.OperatingPlayer));
            }
            else if (route == "fold")
                Assert.AreEqual(0, _games.Fold(42, info.OperatingPlayer));
            if (route != "fold")
            {
                info.Stage = GameStage.River;
                for (int i = 0; i < 5; i++) info.Community.Add(info.Deck.Dequeue());
            }
            var before = _raw.ReconcileChips();
            Sql("CREATE TRIGGER fail_settlement BEFORE UPDATE OF chips ON player_accounts WHEN NEW.chips > OLD.chips BEGIN SELECT RAISE(ABORT, 'Injected settlement failure'); END;");
            _games.NextStage(42);
            Assert.AreEqual(1, _failures);
            Assert.IsTrue(_guard.HasFailed);
            Assert.IsNull(_games.GetGameInfo(42));
            Assert.IsNull(_games.GetGameInfo(43));
            Assert.IsFalse(info.Timer.IsCounting);
            Assert.IsFalse(other.Timer.IsCounting);
            Assert.IsNull(info.Timer.OnTimeUp);
            Assert.AreEqual(0, completed);
            Assert.AreEqual(-1, _games.NextStage(42));
            Assert.AreEqual(-1, _games.StartNew(42));
            Assert.Throws<InvalidOperationException>(() => _guard.AbortHand(info.HandId, "must not write"));
            var failed = _raw.ReconcileChips();
            Assert.IsTrue(failed.IsConsistent);
            Assert.AreEqual(before.AccountBalanceTotal, failed.AccountBalanceTotal);
            Assert.AreEqual(before.LedgerEntryCount, failed.LedgerEntryCount);
            Assert.AreEqual(2, failed.ActiveHandCount);

            Sql("DROP TRIGGER fail_settlement;");
            var restarted = new SqliteAccountRepository(_path);
            restarted.Initialize();
            var recovered = restarted.ReconcileChips();
            Assert.IsTrue(recovered.IsConsistent);
            Assert.AreEqual(4000, recovered.AccountBalanceTotal);
            Assert.AreEqual(0, recovered.ActiveHandCount);
            restarted.Initialize();
            Assert.AreEqual(recovered.LedgerEntryCount, restarted.ReconcileChips().LedgerEntryCount);
        }

        [TestCase("registration")]
        [TestCase("session")]
        public void AuthenticationStorageFailure_IsReportedEvenWhenAccountServiceCatchesIt(string operation)
        {
            var table = operation == "registration" ? "player_accounts" : "account_sessions";
            Sql("ALTER TABLE " + table + " RENAME TO unavailable_accounts;");
            var service = new AccountService(_guard);
            var result = service.Register("NewPlayer", "Password123!");
            Assert.AreEqual(AccountAuthenticationCode.StorageUnavailable, result.Code);
            Assert.IsTrue(_guard.HasFailed);
            Assert.AreEqual(1, _failures);
            Assert.Throws<InvalidOperationException>(() => _guard.SetLastRoom("A", 42));
            Assert.AreEqual(1, _failures);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DebitStorageFailure_StopsPlayWithoutAdditionalLedgerWrites(bool blinds)
        {
            var info = Start();
            if (blinds)
            {
                _games.Fold(42, info.OperatingPlayer);
                _games.NextStage(42);
            }
            Sql("CREATE TRIGGER fail_debit BEFORE UPDATE OF chips ON player_accounts WHEN NEW.chips < OLD.chips BEGIN SELECT RAISE(ABORT, 'Injected debit failure'); END;");
            var before = _raw.ReconcileChips();
            if (blinds) _games.StartNew(42);
            else _games.Call(42, info.OperatingPlayer);
            Assert.AreEqual(1, _failures);
            Assert.IsTrue(_guard.HasFailed);
            Assert.IsNull(_games.GetGameInfo(42));
            Assert.IsFalse(info.Timer.IsCounting);
            var after = _raw.ReconcileChips();
            Assert.AreEqual(before.AccountBalanceTotal, after.AccountBalanceTotal);
            Assert.AreEqual(before.LedgerEntryCount, after.LedgerEntryCount);
            Assert.IsTrue(after.IsConsistent);
        }

        [Test]
        public void RejectedSettlement_IsFatalInsteadOfLeavingAutoRunoutStuck()
        {
            Start();
            Assert.Throws<InvalidOperationException>(() => _guard.SettleHand(
                "missing-hand", "missing-hand:settlement", new HandChipCredit[0], out _));
            Assert.AreEqual(1, _failures);
            Assert.IsNull(_games.GetGameInfo(42));
        }

        [Test]
        public void RoomAssociationWriteFailure_StopsActiveHand()
        {
            var info = Start();
            Sql("CREATE TRIGGER fail_room BEFORE UPDATE OF last_room_id ON player_accounts BEGIN SELECT RAISE(ABORT, 'Injected room failure'); END;");
            Assert.Throws<SqliteException>(() => _guard.SetLastRoom("A", 42));
            Assert.AreEqual(1, _failures);
            Assert.IsFalse(info.Timer.IsCounting);
            Assert.IsNull(_games.GetGameInfo(42));
        }

        [Test]
        public void ExpectedRejections_DoNotStopStorageOrServer()
        {
            var service = new AccountService(_guard);
            Assert.AreEqual(AccountAuthenticationCode.InvalidCredentials, service.Login("NoSuchUser", "wrong").Code);
            Assert.AreEqual(AccountAuthenticationCode.InvalidUserName, service.Register("", "Password123!").Code);
            Assert.AreEqual(CreateAccountResult.UserNameTaken, _guard.Create(_raw.FindById("A")));
            Assert.AreEqual(HandStartResult.InsufficientChips,
                _guard.BeginHand("too-expensive", 42, new[] { "A", "B" }, 1001, out _));
            Assert.AreEqual(0, _failures);
            Assert.IsFalse(_guard.HasFailed);
            Assert.IsTrue(_guard.SetLastRoom("A", 42));
        }

        [Test]
        public void DatabaseInitializationFailure_IsFatalAndReportedOnce()
        {
            var directory = Path.Combine(Path.GetTempPath(), "PokerFatalDir-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var guard = new GuardedAccountRepository(new SqliteAccountRepository(directory), _ => _failures++);
                Assert.Throws<SqliteException>(() => guard.Initialize());
                Assert.Throws<InvalidOperationException>(() => guard.Initialize());
                Assert.IsTrue(guard.HasFailed);
                Assert.AreEqual(1, _failures);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(directory);
            }
        }
    }
}
