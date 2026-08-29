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
