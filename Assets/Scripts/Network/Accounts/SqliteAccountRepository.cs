#if UNITY_EDITOR || UNITY_STANDALONE_LINUX
using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using System.IO;

namespace Game.Network.Accounts
{
    public sealed class SqliteAccountRepository : IAccountRepository
    {
        public const int SchemaVersion = 1;

        private static readonly object RuntimeInitializationLock = new object();
        private static bool _runtimeInitialized;

        private readonly object _syncRoot = new object();
        private readonly string _databasePath;
        private readonly string _connectionString;

        public SqliteAccountRepository(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("A database path is required.", nameof(databasePath));

            _databasePath = Path.GetFullPath(databasePath);
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
            };
            _connectionString = builder.ToString();
        }

        public void Initialize()
        {
            lock (_syncRoot)
            {
                InitializeRuntime();
                var directory = Path.GetDirectoryName(_databasePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction())
                {
                    var version = GetSchemaVersion(connection, transaction);
                    if (version > SchemaVersion)
                    {
                        throw new InvalidOperationException(
                            $"Database schema version {version} is newer than supported version {SchemaVersion}.");
                    }

                    if (version == 0)
                    {
                        CreateSchema(connection, transaction);
                        SetSchemaVersion(connection, transaction, SchemaVersion);
                    }

                    transaction.Commit();
                }
            }
        }

        public PlayerAccount FindById(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
                return null;

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = AccountSelect + " WHERE account_id = $accountId;";
                    command.Parameters.AddWithValue("$accountId", accountId);
                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadAccount(reader) : null;
                    }
                }
            }
        }

        public PlayerAccount FindByUserName(string userName)
        {
            if (userName == null)
                return null;

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = AccountSelect + " WHERE user_name = $userName COLLATE BINARY;";
                    command.Parameters.AddWithValue("$userName", userName);
                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? ReadAccount(reader) : null;
                    }
                }
            }
        }

        public CreateAccountResult Create(PlayerAccount account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = @"
INSERT INTO player_accounts (
    account_id, user_name, password_algorithm, password_hash, password_salt,
    password_iterations, chips, last_room_id, version, created_at_utc, updated_at_utc)
VALUES (
    $accountId, $userName, $passwordAlgorithm, $passwordHash, $passwordSalt,
    $passwordIterations, $chips, $lastRoomId, $version, $createdAtUtc, $updatedAtUtc);";
                            AddAccountParameters(command, account);
                            command.ExecuteNonQuery();
                        }

                        InsertLedger(
                            connection,
                            transaction,
                            Guid.NewGuid().ToString("N"),
                            "register:" + account.AccountId,
                            account.AccountId,
                            "RegistrationGrant",
                            account.Chips,
                            0,
                            account.Chips,
                            null,
                            null,
                            account.CreatedAtUtc);

                        transaction.Commit();
                        return CreateAccountResult.Created;
                    }
                    catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
                    {
                        transaction.Rollback();
                        return CreateAccountResult.UserNameTaken;
                    }
                }
            }
        }

        public bool SetLastRoom(string accountId, int? roomId)
        {
            if (string.IsNullOrEmpty(accountId))
                return false;

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE player_accounts
SET last_room_id = $lastRoomId,
    version = version + 1,
    updated_at_utc = $updatedAtUtc
WHERE account_id = $accountId;";
                    command.Parameters.AddWithValue("$accountId", accountId);
                    command.Parameters.AddWithValue("$lastRoomId", roomId.HasValue ? (object)roomId.Value : DBNull.Value);
                    command.Parameters.AddWithValue("$updatedAtUtc", FormatDateTime(DateTime.UtcNow));
                    return command.ExecuteNonQuery() == 1;
                }
            }
        }

        public ChipMutationResult ApplyChipMutation(
            string accountId,
            string requestId,
            string transactionType,
            long amount,
            int? roomId,
            string handId,
            out PlayerAccount account)
        {
            account = null;
            if (string.IsNullOrEmpty(accountId))
                throw new ArgumentException("An account ID is required.", nameof(accountId));
            if (string.IsNullOrEmpty(requestId))
                throw new ArgumentException("A request ID is required.", nameof(requestId));
            if (string.IsNullOrEmpty(transactionType))
                throw new ArgumentException("A transaction type is required.", nameof(transactionType));

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction())
                {
                    if (LedgerRequestExists(connection, transaction, requestId))
                    {
                        account = FindById(connection, transaction, accountId);
                        transaction.Commit();
                        return ChipMutationResult.AlreadyApplied;
                    }

                    account = FindById(connection, transaction, accountId);
                    if (account == null)
                    {
                        transaction.Rollback();
                        return ChipMutationResult.AccountNotFound;
                    }

                    long newBalance;
                    try
                    {
                        newBalance = checked(account.Chips + amount);
                    }
                    catch (OverflowException)
                    {
                        transaction.Rollback();
                        return ChipMutationResult.Overflow;
                    }

                    if (newBalance < 0)
                    {
                        transaction.Rollback();
                        return ChipMutationResult.InsufficientChips;
                    }

                    var now = DateTime.UtcNow;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
UPDATE player_accounts
SET chips = $chips,
    version = version + 1,
    updated_at_utc = $updatedAtUtc
WHERE account_id = $accountId AND version = $version;";
                        command.Parameters.AddWithValue("$chips", newBalance);
                        command.Parameters.AddWithValue("$updatedAtUtc", FormatDateTime(now));
                        command.Parameters.AddWithValue("$accountId", account.AccountId);
                        command.Parameters.AddWithValue("$version", account.Version);
                        if (command.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("The account changed during a chip transaction.");
                    }

                    InsertLedger(
                        connection,
                        transaction,
                        Guid.NewGuid().ToString("N"),
                        requestId,
                        account.AccountId,
                        transactionType,
                        amount,
                        account.Chips,
                        newBalance,
                        roomId,
                        handId,
                        now);

                    transaction.Commit();
                    account.Chips = newBalance;
                    account.Version++;
                    account.UpdatedAtUtc = now;
                    return ChipMutationResult.Applied;
                }
            }
        }

        private static void InitializeRuntime()
        {
            lock (RuntimeInitializationLock)
            {
                if (_runtimeInitialized)
                    return;

                SQLitePCL.Batteries_V2.Init();
                _runtimeInitialized = true;
            }
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
PRAGMA foreign_keys = ON;
PRAGMA busy_timeout = 5000;";
                command.ExecuteNonQuery();
            }

            return connection;
        }

        private static int GetSchemaVersion(SqliteConnection connection, SqliteTransaction transaction)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "PRAGMA user_version;";
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static void SetSchemaVersion(
            SqliteConnection connection,
            SqliteTransaction transaction,
            int version)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = $"PRAGMA user_version = {version};";
                command.ExecuteNonQuery();
            }
        }

        private static void CreateSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
CREATE TABLE player_accounts (
    account_id TEXT NOT NULL PRIMARY KEY,
    user_name TEXT NOT NULL COLLATE BINARY UNIQUE,
    password_algorithm TEXT NOT NULL,
    password_hash BLOB NOT NULL,
    password_salt BLOB NOT NULL,
    password_iterations INTEGER NOT NULL CHECK (password_iterations > 0),
    chips INTEGER NOT NULL CHECK (chips >= 0),
    last_room_id INTEGER NULL,
    version INTEGER NOT NULL CHECK (version >= 0),
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE chip_ledger (
    transaction_id TEXT NOT NULL PRIMARY KEY,
    request_id TEXT NOT NULL UNIQUE,
    account_id TEXT NOT NULL,
    room_id INTEGER NULL,
    hand_id TEXT NULL,
    transaction_type TEXT NOT NULL,
    amount INTEGER NOT NULL,
    balance_before INTEGER NOT NULL CHECK (balance_before >= 0),
    balance_after INTEGER NOT NULL CHECK (balance_after >= 0),
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (account_id) REFERENCES player_accounts(account_id)
);

CREATE INDEX ix_chip_ledger_account_created
ON chip_ledger(account_id, created_at_utc);";
                command.ExecuteNonQuery();
            }
        }

        private static void AddAccountParameters(SqliteCommand command, PlayerAccount account)
        {
            command.Parameters.AddWithValue("$accountId", account.AccountId);
            command.Parameters.AddWithValue("$userName", account.UserName);
            command.Parameters.AddWithValue("$passwordAlgorithm", account.PasswordAlgorithm);
            command.Parameters.AddWithValue("$passwordHash", account.PasswordHash);
            command.Parameters.AddWithValue("$passwordSalt", account.PasswordSalt);
            command.Parameters.AddWithValue("$passwordIterations", account.PasswordIterations);
            command.Parameters.AddWithValue("$chips", account.Chips);
            command.Parameters.AddWithValue("$lastRoomId", account.LastRoomId.HasValue ? (object)account.LastRoomId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$version", account.Version);
            command.Parameters.AddWithValue("$createdAtUtc", FormatDateTime(account.CreatedAtUtc));
            command.Parameters.AddWithValue("$updatedAtUtc", FormatDateTime(account.UpdatedAtUtc));
        }

        private static bool LedgerRequestExists(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string requestId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT 1 FROM chip_ledger WHERE request_id = $requestId LIMIT 1;";
                command.Parameters.AddWithValue("$requestId", requestId);
                return command.ExecuteScalar() != null;
            }
        }

        private static void InsertLedger(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string transactionId,
            string requestId,
            string accountId,
            string transactionType,
            long amount,
            long balanceBefore,
            long balanceAfter,
            int? roomId,
            string handId,
            DateTime createdAtUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO chip_ledger (
    transaction_id, request_id, account_id, room_id, hand_id, transaction_type,
    amount, balance_before, balance_after, created_at_utc)
VALUES (
    $transactionId, $requestId, $accountId, $roomId, $handId, $transactionType,
    $amount, $balanceBefore, $balanceAfter, $createdAtUtc);";
                command.Parameters.AddWithValue("$transactionId", transactionId);
                command.Parameters.AddWithValue("$requestId", requestId);
                command.Parameters.AddWithValue("$accountId", accountId);
                command.Parameters.AddWithValue("$roomId", roomId.HasValue ? (object)roomId.Value : DBNull.Value);
                command.Parameters.AddWithValue("$handId", handId == null ? (object)DBNull.Value : handId);
                command.Parameters.AddWithValue("$transactionType", transactionType);
                command.Parameters.AddWithValue("$amount", amount);
                command.Parameters.AddWithValue("$balanceBefore", balanceBefore);
                command.Parameters.AddWithValue("$balanceAfter", balanceAfter);
                command.Parameters.AddWithValue("$createdAtUtc", FormatDateTime(createdAtUtc));
                command.ExecuteNonQuery();
            }
        }

        private static PlayerAccount FindById(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string accountId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = AccountSelect + " WHERE account_id = $accountId;";
                command.Parameters.AddWithValue("$accountId", accountId);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? ReadAccount(reader) : null;
                }
            }
        }

        private static PlayerAccount ReadAccount(SqliteDataReader reader)
        {
            return new PlayerAccount
            {
                AccountId = reader.GetString(0),
                UserName = reader.GetString(1),
                PasswordAlgorithm = reader.GetString(2),
                PasswordHash = (byte[])reader[3],
                PasswordSalt = (byte[])reader[4],
                PasswordIterations = reader.GetInt32(5),
                Chips = reader.GetInt64(6),
                LastRoomId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                Version = reader.GetInt64(8),
                CreatedAtUtc = ParseDateTime(reader.GetString(9)),
                UpdatedAtUtc = ParseDateTime(reader.GetString(10)),
            };
        }

        private static string FormatDateTime(DateTime value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        private static DateTime ParseDateTime(string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
        }

        private const string AccountSelect = @"
SELECT account_id, user_name, password_algorithm, password_hash, password_salt,
       password_iterations, chips, last_room_id, version, created_at_utc, updated_at_utc
FROM player_accounts";
    }
}
#endif
