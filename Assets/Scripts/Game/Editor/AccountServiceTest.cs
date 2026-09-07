using Game.Network;
using Game.Network.Accounts;
using Microsoft.Data.Sqlite;
using Mirror;
using NUnit.Framework;
using System;
using System.IO;

namespace Game.Tests
{
    [TestFixture]
    public class AccountServiceTest
    {
        private string _databasePath;

        [SetUp]
        public void SetUp()
        {
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                "PokerAccountTest-" + Guid.NewGuid().ToString("N") + ".db");
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            DeleteIfExists(_databasePath);
            DeleteIfExists(_databasePath + "-shm");
            DeleteIfExists(_databasePath + "-wal");
        }

        [Test]
        public void PasswordHasherUsesUniqueSaltsAndRejectsWrongPassword()
        {
            var hasher = new PasswordHasher(iterations: 1000);
            var first = hasher.HashPassword("correct horse battery staple");
            var second = hasher.HashPassword("correct horse battery staple");
            var account = CreateAccount("Alice", first);

            CollectionAssert.AreNotEqual(first.Salt, second.Salt);
            CollectionAssert.AreNotEqual(first.Hash, second.Hash);
            Assert.IsTrue(hasher.Verify("correct horse battery staple", account));
            Assert.IsFalse(hasher.Verify("wrong password", account));
        }

        [Test]
        public void PasswordLoginMigratesStoredWorkFactor()
        {
            var repository = new SqliteAccountRepository(_databasePath);
            var originalService = new AccountService(
                repository,
                new PasswordHasher(iterations: 2000));
            originalService.Initialize();
            var registration = originalService.Register("MigratingUser", "password123");

            var migratedService = new AccountService(
                repository,
                new PasswordHasher(iterations: 1000));
            var login = migratedService.Login("MigratingUser", "password123");

            Assert.AreEqual(AccountAuthenticationCode.Success, login.Code);
            Assert.AreEqual(1000, repository.FindByUserName("MigratingUser").PasswordIterations);
        }

        [Test]
        public void RegistrationPersistsInitialBalanceAndLedger()
        {
            var service = CreateService();
            var registration = service.Register("Alice", "password123");

            Assert.AreEqual(AccountAuthenticationCode.Success, registration.Code);
            Assert.AreEqual(10000, registration.Account.Chips);

            var reopenedService = CreateService();
            var login = reopenedService.Login("Alice", "password123");
            Assert.AreEqual(AccountAuthenticationCode.Success, login.Code);
            Assert.AreEqual(registration.Account.AccountId, login.Account.AccountId);
            Assert.AreEqual(10000, login.Account.Chips);

            using (var connection = new SqliteConnection("Data Source=" + _databasePath))
            using (var command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT COUNT(*)
FROM chip_ledger
WHERE account_id = $accountId
  AND transaction_type = 'RegistrationGrant'
  AND amount = 10000;";
                command.Parameters.AddWithValue("$accountId", registration.Account.AccountId);
                Assert.AreEqual(1L, (long)command.ExecuteScalar());
            }
        }

        [Test]
        public void SessionTokenRotatesAndLogoutRevokesReplacement()
        {
            var service = CreateService();
            var registration = service.Register("RememberedUser", "password123");

            Assert.AreEqual(AccountAuthenticationCode.Success, registration.Code);
            Assert.IsNotEmpty(registration.SessionToken);

            var resumed = service.LoginWithToken(registration.SessionToken);
            Assert.AreEqual(AccountAuthenticationCode.Success, resumed.Code);
            Assert.AreEqual(registration.Account.AccountId, resumed.Account.AccountId);
            Assert.IsNotEmpty(resumed.SessionToken);
            Assert.AreNotEqual(registration.SessionToken, resumed.SessionToken);
            Assert.AreEqual(
                AccountAuthenticationCode.SessionExpired,
                service.LoginWithToken(registration.SessionToken).Code);

            Assert.IsTrue(service.Logout(resumed.SessionToken));
            Assert.AreEqual(
                AccountAuthenticationCode.SessionExpired,
                service.LoginWithToken(resumed.SessionToken).Code);
        }

        [Test]
        public void SessionTokenExpiresAfterConfiguredLifetime()
        {
            var now = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);
            var service = new AccountService(
                new SqliteAccountRepository(_databasePath),
                new PasswordHasher(iterations: 1000),
                sessionLifetime: TimeSpan.FromDays(30),
                utcNow: () => now);
            service.Initialize();
            var registration = service.Register("ExpiringUser", "password123");

            now = now.AddDays(31);

            Assert.AreEqual(
                AccountAuthenticationCode.SessionExpired,
                service.LoginWithToken(registration.SessionToken).Code);
        }

        [Test]
        public void UserNamesAreCaseSensitiveAndExactDuplicatesAreRejected()
        {
            var service = CreateService();

            Assert.AreEqual(
                AccountAuthenticationCode.Success,
                service.Register("Alice", "password123").Code);
            Assert.AreEqual(
                AccountAuthenticationCode.Success,
                service.Register("alice", "password123").Code);
            Assert.AreEqual(
                AccountAuthenticationCode.UserNameTaken,
                service.Register("Alice", "different123").Code);
            Assert.AreEqual(
                AccountAuthenticationCode.InvalidCredentials,
                service.Login("ALICE", "password123").Code);
        }

        [Test]
        public void ChipMutationIsAtomicAndIdempotent()
        {
            var service = CreateService();
            var account = service.Register("LedgerUser", "password123").Account;
            var repository = new SqliteAccountRepository(_databasePath);
            repository.Initialize();

            var firstResult = repository.ApplyChipMutation(
                account.AccountId,
                "hand-1-settlement",
                "HandSettlement",
                -1500,
                roomId: 7,
                handId: "hand-1",
                out var firstBalance);
            var repeatedResult = repository.ApplyChipMutation(
                account.AccountId,
                "hand-1-settlement",
                "HandSettlement",
                -1500,
                roomId: 7,
                handId: "hand-1",
                out var repeatedBalance);
            var insufficientResult = repository.ApplyChipMutation(
                account.AccountId,
                "too-large",
                "HandSettlement",
                -10000,
                roomId: 7,
                handId: "hand-2",
                out var unchangedBalance);

            Assert.AreEqual(ChipMutationResult.Applied, firstResult);
            Assert.AreEqual(8500, firstBalance.Chips);
            Assert.AreEqual(ChipMutationResult.AlreadyApplied, repeatedResult);
            Assert.AreEqual(8500, repeatedBalance.Chips);
            Assert.AreEqual(ChipMutationResult.InsufficientChips, insufficientResult);
            Assert.AreEqual(8500, unchangedBalance.Chips);
        }

        [Test]
        public void LastRoomPersistsAcrossRepositoryInstances()
        {
            var service = CreateService();
            var account = service.Register("RoomUser", "password123").Account;
            var repository = new SqliteAccountRepository(_databasePath);
            repository.Initialize();

            Assert.IsTrue(repository.SetLastRoom(account.AccountId, 42));

            var reopened = new SqliteAccountRepository(_databasePath);
            reopened.Initialize();
            Assert.AreEqual(42, reopened.FindById(account.AccountId).LastRoomId);
        }

        [Test]
        public void HandTransactionsAreAtomicConservativeAndIdempotent()
        {
            var service = CreateService();
            var alice = service.Register("HandAlice", "password123").Account;
            var bob = service.Register("HandBob", "password123").Account;
            var repository = new SqliteAccountRepository(_databasePath);
            repository.Initialize();

            Assert.AreEqual(
                HandStartResult.Started,
                repository.BeginHand(
                    "hand-atomic",
                    7,
                    new[] { alice.AccountId, bob.AccountId },
                    100,
                    out _));

            Assert.AreEqual(10000, repository.FindById(alice.AccountId).Chips);
            Assert.AreEqual(10000, repository.FindById(bob.AccountId).Chips);
            Assert.AreEqual(
                ChipMutationResult.AccountBusyInHand,
                repository.ApplyChipMutation(
                    alice.AccountId,
                    "mid-hand-grant",
                    "Grant",
                    100,
                    null,
                    null,
                    out _));
            Assert.AreEqual(
                HandStartResult.AccountAlreadyInHand,
                repository.BeginHand(
                    "second-hand",
                    9,
                    new[] { alice.AccountId, bob.AccountId },
                    100,
                    out _));

            var credits = new[]
            {
                new HandSettlementEntry { AccountId = alice.AccountId, Contribution = 150, Award = 200, Refund = 50 },
                new HandSettlementEntry { AccountId = bob.AccountId, Contribution = 100 },
            };
            Assert.AreEqual(
                HandSettlementResult.Applied,
                repository.SettleHand("hand-atomic", "settlement-1", credits, out var settled));
            Assert.AreEqual(10100, settled[alice.AccountId].Chips);
            Assert.AreEqual(
                HandSettlementResult.AlreadyApplied,
                repository.SettleHand("hand-atomic", "settlement-1", credits, out var repeatedSettlement));
            Assert.AreEqual(10100, repeatedSettlement[alice.AccountId].Chips);
            Assert.AreEqual(
                20000,
                (long)repository.FindById(alice.AccountId).Chips + repository.FindById(bob.AccountId).Chips);
        }

        [Test]
        public void InitializeAbortsAnUnsettledHandWithoutChangingBalances()
        {
            var service = CreateService();
            var alice = service.Register("RecoveryAlice", "password123").Account;
            var bob = service.Register("RecoveryBob", "password123").Account;
            var repository = new SqliteAccountRepository(_databasePath);
            repository.Initialize();
            Assert.AreEqual(
                HandStartResult.Started,
                repository.BeginHand(
                    "hand-recovery",
                    8,
                    new[] { alice.AccountId, bob.AccountId },
                    100,
                    out _));


            var restartedRepository = new SqliteAccountRepository(_databasePath);
            restartedRepository.Initialize();

            Assert.AreEqual(10000, restartedRepository.FindById(alice.AccountId).Chips);
            Assert.AreEqual(10000, restartedRepository.FindById(bob.AccountId).Chips);
            Assert.IsFalse(restartedRepository.AbortHand("hand-recovery", "RepeatedAbort"));
            var report = restartedRepository.ReconcileChips();
            Assert.IsTrue(report.IsConsistent);
            Assert.AreEqual(0, report.ActiveHandCount);
            Assert.AreEqual(0, report.ActiveContributionTotal);
            Assert.AreEqual(0, report.InternalHandLedgerTotal);
        }

        [Test]
        public void ReconciliationTracksDeferredSettlementAndRecovery()
        {
            var service = CreateService();
            var alice = service.Register("AuditAlice", "password123").Account;
            var bob = service.Register("AuditBob", "password123").Account;
            var repository = new SqliteAccountRepository(_databasePath);
            repository.Initialize();

            Assert.AreEqual(
                HandStartResult.Started,
                repository.BeginHand(
                    "hand-audit",
                    11,
                    new[] { alice.AccountId, bob.AccountId },
                    100,
                    out _));


            var activeReport = repository.ReconcileChips();
            Assert.IsTrue(activeReport.IsConsistent);
            Assert.AreEqual(20000, activeReport.AccountBalanceTotal);
            Assert.AreEqual(20000, activeReport.LedgerNetTotal);
            Assert.AreEqual(0, activeReport.ActiveContributionTotal);
            Assert.AreEqual(0, activeReport.InternalHandLedgerTotal);
            Assert.AreEqual(1, activeReport.ActiveHandCount);

            Assert.AreEqual(
                HandSettlementResult.Applied,
                repository.SettleHand(
                    "hand-audit",
                    "audit-settlement",
                    new[] { new HandSettlementEntry { AccountId = alice.AccountId, Contribution = 300, Refund = 100 },
                        new HandSettlementEntry { AccountId = bob.AccountId, Contribution = 200, Award = 400 } },
                    out _));

            var settledReport = repository.ReconcileChips();
            Assert.IsTrue(settledReport.IsConsistent);
            Assert.AreEqual(20000, settledReport.AccountBalanceTotal);
            Assert.AreEqual(20000, settledReport.LedgerNetTotal);
            Assert.AreEqual(0, settledReport.ActiveContributionTotal);
            Assert.AreEqual(0, settledReport.InternalHandLedgerTotal);
            Assert.AreEqual(0, settledReport.ActiveHandCount);
        }

        private AccountService CreateService()
        {
            var service = new AccountService(
                new SqliteAccountRepository(_databasePath),
                new PasswordHasher(iterations: 1000));
            service.Initialize();
            return service;
        }

        private static PlayerAccount CreateAccount(string userName, PasswordHashData passwordData)
        {
            return new PlayerAccount
            {
                AccountId = Guid.NewGuid().ToString("N"),
                UserName = userName,
                PasswordAlgorithm = passwordData.Algorithm,
                PasswordHash = passwordData.Hash,
                PasswordSalt = passwordData.Salt,
                PasswordIterations = passwordData.Iterations,
            };
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestFixture]
    public class PlayerManagerSessionTest
    {
        [Test]
        public void NewLoginReplacesOldConnectionWithoutAllowingStaleDisconnectToRemoveIt()
        {
            var manager = new PlayerManager();
            var account = new PlayerAccount
            {
                AccountId = "account-1",
                UserName = "Alice",
                Chips = 10000,
            };
            var oldConnection = new NetworkConnectionToClient(1);
            var newConnection = new NetworkConnectionToClient(2);

            Assert.IsNull(manager.BindAccount(account, oldConnection));
            Assert.AreSame(oldConnection, manager.BindAccount(account, newConnection));
            Assert.AreSame(newConnection, manager.GetConnection("Alice"));
            Assert.IsFalse(manager.RemoveConnection(oldConnection, out _));
            Assert.IsTrue(manager.IsConnectionForPlayer("Alice", newConnection));
            Assert.IsTrue(manager.RemoveConnection(newConnection, out var playerName));
            Assert.AreEqual("Alice", playerName);
            Assert.IsFalse(manager.IsConnected("Alice"));
        }
    }
}
