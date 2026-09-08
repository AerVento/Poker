#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository :
        IHandAccountRepository,
        ISessionAccountRepository,
        IPasswordAccountRepository,
        IDailyRewardRepository,
        ILeaderboardRepository
    {
        public const int SchemaVersion = 9;

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
                        version = 1;
                    }

                    if (version < 2)
                        CreateHandSchema(connection, transaction);

                    if (version < 3)
                        CreateSessionSchema(connection, transaction);

                    if (version < 4)
                        CreateDeferredSettlementSchema(connection, transaction);

                    if (version < 5)
                        CreateShopSchema(connection, transaction);

                    if (version < 6)
                        CreateItemSchema(connection, transaction);

                    if (version < 7)
                        CreateTaskSchema(connection, transaction);

                    if (version < 8)
                        CreateItemOutcomeSchema(connection, transaction);

                    if (version < 9)
                        CreateItemGrantSchema(connection, transaction);

                    SetSchemaVersion(connection, transaction, SchemaVersion);

                    transaction.Commit();
                }

                RecoverAbandonedHands();
            }
        }

        public LeaderboardSnapshot ReadLeaderboard(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
                throw new ArgumentException("An authenticated account is required.", nameof(accountId));
            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction(deferred: true))
                {
                    var top = new List<LeaderboardEntry>(10);
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
SELECT account_id, user_name, chips, equipped_namecard, " + ServiceYearsSql("player_accounts.account_id") + @" FROM player_accounts
ORDER BY chips DESC, created_at_utc ASC, account_id COLLATE BINARY ASC LIMIT 10;";
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                                top.Add(new LeaderboardEntry
                                {
                                    Rank = top.Count + 1, UserName = reader.GetString(1),
                                    NamecardId = reader.GetString(3), ServiceYears = checked((ulong)reader.GetInt64(4)),
                                    Chips = checked((uint)reader.GetInt64(2)), IsSelf = reader.GetString(0) == accountId,
                                });
                        }
                    }
                    LeaderboardEntry self;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
SELECT me.user_name, me.chips, 1 + (
    SELECT COUNT(*) FROM player_accounts AS other
    WHERE other.chips > me.chips
       OR (other.chips = me.chips AND other.created_at_utc < me.created_at_utc)
       OR (other.chips = me.chips AND other.created_at_utc = me.created_at_utc
           AND other.account_id COLLATE BINARY < me.account_id COLLATE BINARY)), me.equipped_namecard, " + ServiceYearsSql("me.account_id") + @"
FROM player_accounts AS me WHERE me.account_id = $accountId;";
                        command.Parameters.AddWithValue("$accountId", accountId);
                        using (var reader = command.ExecuteReader())
                        {
                            if (!reader.Read())
                                throw new InvalidOperationException("The authenticated account no longer exists.");
                            self = new LeaderboardEntry
                            {
                                UserName = reader.GetString(0), Chips = checked((uint)reader.GetInt64(1)),
                                Rank = reader.GetInt64(2), IsSelf = true, NamecardId = reader.GetString(3), ServiceYears = checked((ulong)reader.GetInt64(4)),
                            };
                        }
                    }
                    // Both tabs share one consistent read transaction, so tab switches need no extra request.
                    var service = ReadServiceLeaderboard(connection, transaction, accountId);
                    transaction.Commit();
                    return new LeaderboardSnapshot
                    {
                        TopPlayers = top.ToArray(), Self = self,
                        ServiceTopPlayers = service.TopPlayers, ServiceSelf = service.Self,
                    };
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

        public bool UpdatePasswordHash(string accountId, PasswordHashData passwordData)
        {
            if (string.IsNullOrEmpty(accountId) ||
                passwordData == null ||
                string.IsNullOrEmpty(passwordData.Algorithm) ||
                passwordData.Hash == null ||
                passwordData.Salt == null ||
                passwordData.Iterations <= 0)
            {
                return false;
            }

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE player_accounts
SET password_algorithm = $passwordAlgorithm,
    password_hash = $passwordHash,
    password_salt = $passwordSalt,
    password_iterations = $passwordIterations,
    updated_at_utc = $updatedAtUtc
WHERE account_id = $accountId;";
                    command.Parameters.AddWithValue("$accountId", accountId);
                    command.Parameters.AddWithValue("$passwordAlgorithm", passwordData.Algorithm);
                    command.Parameters.AddWithValue("$passwordHash", passwordData.Hash);
                    command.Parameters.AddWithValue("$passwordSalt", passwordData.Salt);
                    command.Parameters.AddWithValue("$passwordIterations", passwordData.Iterations);
                    command.Parameters.AddWithValue("$updatedAtUtc", FormatDateTime(DateTime.UtcNow));
                    return command.ExecuteNonQuery() == 1;
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

        public bool CreateSessionToken(
            string accountId,
            byte[] tokenHash,
            DateTime createdAtUtc,
            DateTime expiresAtUtc)
        {
            if (string.IsNullOrEmpty(accountId))
                throw new ArgumentException("An account id is required.", nameof(accountId));
            ValidateTokenHash(tokenHash);

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction())
                {
                    if (FindById(connection, transaction, accountId) == null)
                    {
                        transaction.Rollback();
                        return false;
                    }

                    InsertSessionToken(
                        connection,
                        transaction,
                        accountId,
                        tokenHash,
                        createdAtUtc,
                        expiresAtUtc);
                    transaction.Commit();
                    return true;
                }
            }
        }

        public SessionTokenRotationResult RotateSessionToken(
            byte[] currentTokenHash,
            byte[] replacementTokenHash,
            DateTime usedAtUtc,
            DateTime replacementExpiresAtUtc,
            out PlayerAccount account)
        {
            ValidateTokenHash(currentTokenHash);
            ValidateTokenHash(replacementTokenHash);
            account = null;

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction())
                {
                    string sessionId = null;
                    string accountId = null;
                    DateTime expiresAtUtc = default;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
SELECT session_id, account_id, expires_at_utc
FROM account_sessions
WHERE token_hash = $tokenHash AND revoked_at_utc IS NULL;";
                        command.Parameters.AddWithValue("$tokenHash", currentTokenHash);
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                sessionId = reader.GetString(0);
                                accountId = reader.GetString(1);
                                expiresAtUtc = ParseDateTime(reader.GetString(2));
                            }
                        }
                    }

                    if (sessionId == null || expiresAtUtc <= usedAtUtc.ToUniversalTime())
                    {
                        if (sessionId != null)
                            RevokeSession(connection, transaction, sessionId, usedAtUtc);
                        transaction.Commit();
                        return SessionTokenRotationResult.InvalidToken;
                    }

                    account = FindById(connection, transaction, accountId);
                    if (account == null)
                    {
                        transaction.Rollback();
                        return SessionTokenRotationResult.AccountNotFound;
                    }

                    RevokeSession(connection, transaction, sessionId, usedAtUtc);
                    InsertSessionToken(
                        connection,
                        transaction,
                        accountId,
                        replacementTokenHash,
                        usedAtUtc,
                        replacementExpiresAtUtc);
                    transaction.Commit();
                    return SessionTokenRotationResult.Rotated;
                }
            }
        }

        public bool RevokeSessionToken(byte[] tokenHash, DateTime revokedAtUtc)
        {
            ValidateTokenHash(tokenHash);
            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE account_sessions
SET revoked_at_utc = $revokedAtUtc
WHERE token_hash = $tokenHash AND revoked_at_utc IS NULL;";
                    command.Parameters.AddWithValue("$revokedAtUtc", revokedAtUtc.ToUniversalTime().ToString("O"));
                    command.Parameters.AddWithValue("$tokenHash", tokenHash);
                    return command.ExecuteNonQuery() == 1;
                }
            }
        }

        public DailyRewardAccountState ReadDailyRewards(string accountId, string supplyKey, string comebackKey)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                var state = new DailyRewardAccountState
                {
                    Account = FindById(connection, transaction, accountId),
                    HasActiveHand = IsAccountInActiveHand(connection, transaction, accountId),
                    SupplyClaimed = LedgerRequestExists(connection, transaction, supplyKey),
                    ComebackClaimed = LedgerRequestExists(connection, transaction, comebackKey),
                };
                transaction.Commit();
                return state;
            }
        }

        public ChipMutationResult ClaimDailyReward(string accountId, string claimKey, uint amount,
            uint? maximumBalanceInclusive, out PlayerAccount account)
        {
            return ApplyChipMutationCore(accountId, claimKey, "DailyTaskReward", amount, null, null,
                maximumBalanceInclusive, true, out account);
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
            return ApplyChipMutationCore(accountId, requestId, transactionType, amount, roomId, handId,
                null, false, out account);
        }

        private ChipMutationResult ApplyChipMutationCore(string accountId, string requestId,
            string transactionType, long amount, int? roomId, string handId,
            uint? maximumBalanceInclusive, bool rejectActiveHandFirst, out PlayerAccount account)
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
                    if (rejectActiveHandFirst && IsAccountInActiveHand(connection, transaction, accountId))
                    {
                        transaction.Rollback();
                        return ChipMutationResult.AccountBusyInHand;
                    }
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
                    if (IsAccountInActiveHand(connection, transaction, accountId))
                    {
                        transaction.Rollback();
                        return ChipMutationResult.AccountBusyInHand;
                    }

                    if (maximumBalanceInclusive.HasValue && account.Chips > maximumBalanceInclusive.Value)
                    {
                        transaction.Rollback();
                        return ChipMutationResult.BalanceRequirementNotMet;
                    }

                    long newBalance;
                    try
                    {
                        newBalance = checked((long)account.Chips + amount);
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
                    if (newBalance > uint.MaxValue)
                    {
                        transaction.Rollback();
                        return ChipMutationResult.Overflow;
                    }

                    var newChipBalance = (uint)newBalance;

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
                        command.Parameters.AddWithValue("$chips", (long)newChipBalance);
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
                        newChipBalance,
                        roomId,
                        handId,
                        now);

                    transaction.Commit();
                    account.Chips = newChipBalance;
                    account.Version++;
                    account.UpdatedAtUtc = now;
                    return ChipMutationResult.Applied;
                }
            }
        }

        public HandStartResult BeginHand(
            string handId,
            int roomId,
            IReadOnlyList<string> accountIds,
            uint minimumChips,
            out IReadOnlyList<PlayerAccount> accounts)
        {
            accounts = Array.Empty<PlayerAccount>();
            if (string.IsNullOrEmpty(handId))
                throw new ArgumentException("A hand ID is required.", nameof(handId));
            if (accountIds == null || accountIds.Count < 2)
                throw new ArgumentException("At least two accounts are required.", nameof(accountIds));
            if (accountIds.Any(string.IsNullOrEmpty) || accountIds.Distinct().Count() != accountIds.Count)
                throw new ArgumentException("Hand account IDs must be non-empty and unique.", nameof(accountIds));

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction())
                {
                    if (GetHandStatus(connection, transaction, handId) != null)
                    {
                        transaction.Commit();
                        return HandStartResult.AlreadyExists;
                    }

                    var snapshots = new List<PlayerAccount>(accountIds.Count);
                    ulong totalChips = 0;
                    foreach (var accountId in accountIds)
                    {
                        var account = FindById(connection, transaction, accountId);
                        if (account == null)
                        {
                            transaction.Rollback();
                            return HandStartResult.AccountNotFound;
                        }
                        if (IsAccountInActiveHand(connection, transaction, accountId))
                        {
                            transaction.Rollback();
                            return HandStartResult.AccountAlreadyInHand;
                        }
                        if (account.Chips < minimumChips)
                        {
                            transaction.Rollback();
                            return HandStartResult.InsufficientChips;
                        }

                        snapshots.Add(account);
                        totalChips = checked(totalChips + account.Chips);
                    }

                    if (totalChips > uint.MaxValue)
                    {
                        transaction.Rollback();
                        return HandStartResult.BalanceLimitExceeded;
                    }

                    var now = DateTime.UtcNow;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
INSERT INTO poker_hands (hand_id, room_id, status, created_at_utc, completed_at_utc, accounting_mode)
VALUES ($handId, $roomId, 'Active', $createdAtUtc, NULL, 1);";
                        command.Parameters.AddWithValue("$handId", handId);
                        command.Parameters.AddWithValue("$roomId", roomId);
                        command.Parameters.AddWithValue("$createdAtUtc", FormatDateTime(now));
                        command.ExecuteNonQuery();
                    }

                    foreach (var account in snapshots)
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = @"
INSERT INTO poker_hand_participants (hand_id, account_id, starting_chips, contributed_chips)
VALUES ($handId, $accountId, $startingChips, 0);";
                            command.Parameters.AddWithValue("$handId", handId);
                            command.Parameters.AddWithValue("$accountId", account.AccountId);
                            command.Parameters.AddWithValue("$startingChips", (long)account.Chips);
                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                    accounts = snapshots;
                    return HandStartResult.Started;
                }
            }
        }

        public HandSettlementResult SettleHand(
            string handId,
            string requestId,
            IReadOnlyList<HandSettlementEntry> entries,
            out IReadOnlyDictionary<string, HandSettlementBalance> accounts)
        {
            accounts = new Dictionary<string, HandSettlementBalance>();
            if (string.IsNullOrEmpty(handId) || string.IsNullOrEmpty(requestId))
                throw new ArgumentException("Hand and request IDs are required.");
            if (entries == null || entries.Count < 2 || entries.Any(entry =>
                entry == null || string.IsNullOrEmpty(entry.AccountId)) ||
                entries.Select(entry => entry.AccountId).Distinct(StringComparer.Ordinal).Count() != entries.Count)
                return HandSettlementResult.InvalidDistribution;

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction())
                {
                    var status = GetHandStatus(connection, transaction, handId);
                    if ((status != "Active" && status != "Settled") ||
                        GetHandAccountingMode(connection, transaction, handId) != 1)
                        return HandSettlementResult.HandNotActive;

                    var stored = new Dictionary<string, (uint Starting, uint Contribution, uint Award, uint Refund, uint? Final)>(StringComparer.Ordinal);
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = @"
SELECT account_id, starting_chips, contributed_chips, awarded_chips, refunded_chips, settled_chips
FROM poker_hand_participants WHERE hand_id = $handId;";
                        command.Parameters.AddWithValue("$handId", handId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                                stored.Add(reader.GetString(0), (checked((uint)reader.GetInt64(1)),
                                    checked((uint)reader.GetInt64(2)), checked((uint)reader.GetInt64(3)),
                                    checked((uint)reader.GetInt64(4)), reader.IsDBNull(5) ? (uint?)null : checked((uint)reader.GetInt64(5))));
                        }
                    }
                    if (stored.Count != entries.Count)
                        return HandSettlementResult.InvalidDistribution;

                    ulong contributions = 0;
                    ulong credits = 0;
                    var balances = new Dictionary<string, HandSettlementBalance>(StringComparer.Ordinal);
                    foreach (var entry in entries)
                    {
                        if (!stored.TryGetValue(entry.AccountId, out var participant) ||
                            entry.Contribution > participant.Starting || entry.Refund > entry.Contribution)
                            return HandSettlementResult.InvalidDistribution;
                        var final = (long)participant.Starting - entry.Contribution + entry.Award + entry.Refund;
                        if (final < 0 || final > uint.MaxValue)
                            return HandSettlementResult.InvalidDistribution;
                        if (status == "Settled" && (participant.Contribution != entry.Contribution ||
                            participant.Award != entry.Award || participant.Refund != entry.Refund || participant.Final != (uint)final))
                            return HandSettlementResult.InvalidDistribution;
                        contributions = checked(contributions + entry.Contribution);
                        credits = checked(credits + entry.Award + entry.Refund);
                        balances.Add(entry.AccountId, new HandSettlementBalance
                        {
                            AccountId = entry.AccountId, StartingChips = participant.Starting, Chips = (uint)final,
                        });
                    }
                    if (contributions != credits)
                        return HandSettlementResult.InvalidDistribution;
                    if (status == "Settled")
                    {
                        // Return the recorded hand result, even if the account has since played again.
                        transaction.Commit();
                        accounts = balances;
                        return HandSettlementResult.AlreadyApplied;
                    }
                    if (HandRequestExists(connection, transaction, requestId))
                        return HandSettlementResult.InvalidDistribution;

                    var now = DateTime.UtcNow;
                    var roomId = GetHandRoomId(connection, transaction, handId);
                    foreach (var entry in entries.OrderBy(entry => entry.AccountId, StringComparer.Ordinal))
                    {
                        var account = FindById(connection, transaction, entry.AccountId);
                        var balance = balances[entry.AccountId];
                        if (account == null || account.Chips != balance.StartingChips)
                            return HandSettlementResult.InvalidDistribution;
                        if (balance.Chips != account.Chips)
                        {
                            UpdateAccountBalance(connection, transaction, account, balance.Chips, now);
                            InsertLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                                "hand-net:" + handId + ":" + entry.AccountId, entry.AccountId, "HandNetSettlement",
                                (long)balance.Chips - account.Chips, account.Chips, balance.Chips, roomId, handId, now);
                        }
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = @"
UPDATE poker_hand_participants
SET contributed_chips = $contribution, awarded_chips = $award, refunded_chips = $refund, settled_chips = $final
WHERE hand_id = $handId AND account_id = $accountId;";
                            command.Parameters.AddWithValue("$handId", handId);
                            command.Parameters.AddWithValue("$accountId", entry.AccountId);
                            command.Parameters.AddWithValue("$contribution", (long)entry.Contribution);
                            command.Parameters.AddWithValue("$award", (long)entry.Award);
                            command.Parameters.AddWithValue("$refund", (long)entry.Refund);
                            command.Parameters.AddWithValue("$final", (long)balance.Chips);
                            if (command.ExecuteNonQuery() != 1)
                                throw new InvalidOperationException("The hand participant could not be settled.");
                        }
                    }
                    foreach (var entry in entries)
                    {
                        if (entry.OutcomeFact != null)
                        {
                            if (entry.OutcomeFact.HandId != handId || entry.OutcomeFact.AccountId != entry.AccountId ||
                                entry.OutcomeFact.Kind != Game.Facts.HandFactKind.Settled)
                                throw new InvalidOperationException("Settlement fact identity mismatch.");
                            InsertHandFact(connection, transaction, entry.OutcomeFact);
                        }
                    }
                    MarkHandCompleted(connection, transaction, handId, "Settled", now);
                    InsertHandRequest(connection, transaction, requestId, handId, "NetSettlement", now);
                    transaction.Commit();
                    accounts = balances;
                    return HandSettlementResult.Applied;
                }
            }
        }

        public bool AbortHand(string handId, string reason)
        {
            if (string.IsNullOrEmpty(handId))
                return false;

            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction())
                {
                    if (GetHandStatus(connection, transaction, handId) != "Active")
                    {
                        transaction.Rollback();
                        return false;
                    }

                    AbortStoredHand(connection, transaction, handId, reason, DateTime.UtcNow);
                    transaction.Commit();
                    return true;
                }
            }
        }

        public ChipReconciliationReport ReconcileChips()
        {
            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                {
                    return new ChipReconciliationReport
                    {
                        AccountBalanceTotal = ReadInt64(
                            connection,
                            "SELECT COALESCE(SUM(chips), 0) FROM player_accounts;"),
                        LedgerNetTotal = ReadInt64(
                            connection,
                            "SELECT COALESCE(SUM(amount), 0) FROM chip_ledger;"),
                        ActiveContributionTotal = ReadInt64(
                            connection,
                            @"SELECT COALESCE(SUM(participant.contributed_chips), 0)
FROM poker_hand_participants AS participant
INNER JOIN poker_hands AS hand ON hand.hand_id = participant.hand_id
WHERE hand.status = 'Active';"),
                        InternalHandLedgerTotal = ReadInt64(
                            connection,
                            @"SELECT COALESCE(SUM(amount), 0)
FROM chip_ledger
WHERE transaction_type IN (
    'HandCommit', 'HandSettlement', 'HandNetSettlement', 'CrashRecoveryRefund', 'HandAbortRefund');"),
                        ActiveHandCount = ReadInt64(
                            connection,
                            "SELECT COUNT(*) FROM poker_hands WHERE status = 'Active';"),
                        LedgerEntryCount = ReadInt64(
                            connection,
                            "SELECT COUNT(*) FROM chip_ledger;"),
                        InvalidLedgerEntryCount = ReadInt64(
                            connection,
                            "SELECT COUNT(*) FROM chip_ledger WHERE balance_after - balance_before <> amount;"),
                        AccountLedgerMismatchCount = ReadInt64(
                            connection,
                            @"SELECT COUNT(*)
FROM (
    SELECT account.account_id
    FROM player_accounts AS account
    LEFT JOIN chip_ledger AS ledger ON ledger.account_id = account.account_id
    GROUP BY account.account_id, account.chips
    HAVING account.chips <> COALESCE(SUM(ledger.amount), 0)
);"),
                        InvalidActiveContributionCount = ReadInt64(
                            connection,
                            @"SELECT COUNT(*)
FROM poker_hand_participants AS participant
INNER JOIN poker_hands AS hand ON hand.hand_id = participant.hand_id
WHERE hand.status = 'Active'
  AND participant.contributed_chips > participant.starting_chips;"),
                    };
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

        private static long ReadInt64(SqliteConnection connection, string commandText)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
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
    chips INTEGER NOT NULL CHECK (chips >= 0 AND chips <= 4294967295),
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
    balance_before INTEGER NOT NULL CHECK (balance_before >= 0 AND balance_before <= 4294967295),
    balance_after INTEGER NOT NULL CHECK (balance_after >= 0 AND balance_after <= 4294967295),
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (account_id) REFERENCES player_accounts(account_id)
);

CREATE INDEX ix_chip_ledger_account_created
ON chip_ledger(account_id, created_at_utc);";
                command.ExecuteNonQuery();
            }
        }

        private static void CreateHandSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
CREATE TABLE poker_hands (
    hand_id TEXT NOT NULL PRIMARY KEY,
    room_id INTEGER NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('Active', 'Settled', 'Aborted')),
    created_at_utc TEXT NOT NULL,
    completed_at_utc TEXT NULL
);

CREATE TABLE poker_hand_participants (
    hand_id TEXT NOT NULL,
    account_id TEXT NOT NULL,
    starting_chips INTEGER NOT NULL CHECK (starting_chips >= 0 AND starting_chips <= 4294967295),
    contributed_chips INTEGER NOT NULL CHECK (contributed_chips >= 0 AND contributed_chips <= 4294967295),
    PRIMARY KEY (hand_id, account_id),
    FOREIGN KEY (hand_id) REFERENCES poker_hands(hand_id),
    FOREIGN KEY (account_id) REFERENCES player_accounts(account_id)
);

CREATE TABLE hand_requests (
    request_id TEXT NOT NULL PRIMARY KEY,
    hand_id TEXT NOT NULL,
    request_type TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (hand_id) REFERENCES poker_hands(hand_id)
);

CREATE INDEX ix_poker_hands_status ON poker_hands(status);";
                command.ExecuteNonQuery();
            }
        }

        private static void CreateDeferredSettlementSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            // Execute each DDL statement separately: this provider can stop a multi-statement
            // command on a later schema error without propagating that error to ExecuteNonQuery.
            var statements = new[]
            {
                "ALTER TABLE poker_hands ADD COLUMN accounting_mode INTEGER NOT NULL DEFAULT 0 CHECK (accounting_mode IN (0, 1));",
                "ALTER TABLE poker_hand_participants ADD COLUMN awarded_chips INTEGER NOT NULL DEFAULT 0 CHECK (awarded_chips BETWEEN 0 AND 4294967295);",
                "ALTER TABLE poker_hand_participants ADD COLUMN refunded_chips INTEGER NOT NULL DEFAULT 0 CHECK (refunded_chips BETWEEN 0 AND 4294967295);",
                "ALTER TABLE poker_hand_participants ADD COLUMN settled_chips INTEGER NULL CHECK (settled_chips BETWEEN 0 AND 4294967295);",
                "CREATE INDEX ix_player_accounts_leaderboard ON player_accounts(chips DESC, created_at_utc ASC, account_id ASC);",
            };
            foreach (var statement in statements)
            {
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = statement;
                    command.ExecuteNonQuery();
                }
            }
        }

        private static int GetHandAccountingMode(SqliteConnection connection, SqliteTransaction transaction, string handId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT accounting_mode FROM poker_hands WHERE hand_id = $handId;";
                command.Parameters.AddWithValue("$handId", handId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private static void CreateSessionSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
CREATE TABLE account_sessions (
    session_id TEXT NOT NULL PRIMARY KEY,
    account_id TEXT NOT NULL,
    token_hash BLOB NOT NULL UNIQUE,
    expires_at_utc TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    last_used_at_utc TEXT NOT NULL,
    revoked_at_utc TEXT NULL,
    FOREIGN KEY (account_id) REFERENCES player_accounts(account_id)
);

CREATE INDEX ix_account_sessions_account
ON account_sessions(account_id, revoked_at_utc, expires_at_utc);";
                command.ExecuteNonQuery();
            }
        }

        private static void InsertSessionToken(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string accountId,
            byte[] tokenHash,
            DateTime createdAtUtc,
            DateTime expiresAtUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO account_sessions (
    session_id, account_id, token_hash, expires_at_utc,
    created_at_utc, last_used_at_utc, revoked_at_utc)
VALUES (
    $sessionId, $accountId, $tokenHash, $expiresAtUtc,
    $createdAtUtc, $createdAtUtc, NULL);";
                command.Parameters.AddWithValue("$sessionId", Guid.NewGuid().ToString("N"));
                command.Parameters.AddWithValue("$accountId", accountId);
                command.Parameters.AddWithValue("$tokenHash", tokenHash);
                command.Parameters.AddWithValue("$expiresAtUtc", expiresAtUtc.ToUniversalTime().ToString("O"));
                command.Parameters.AddWithValue("$createdAtUtc", createdAtUtc.ToUniversalTime().ToString("O"));
                command.ExecuteNonQuery();
            }
        }

        private static void RevokeSession(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string sessionId,
            DateTime revokedAtUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE account_sessions
SET revoked_at_utc = $revokedAtUtc, last_used_at_utc = $revokedAtUtc
WHERE session_id = $sessionId AND revoked_at_utc IS NULL;";
                command.Parameters.AddWithValue("$revokedAtUtc", revokedAtUtc.ToUniversalTime().ToString("O"));
                command.Parameters.AddWithValue("$sessionId", sessionId);
                command.ExecuteNonQuery();
            }
        }

        private static void ValidateTokenHash(byte[] tokenHash)
        {
            if (tokenHash == null || tokenHash.Length != 32)
                throw new ArgumentException("A SHA-256 session token hash is required.", nameof(tokenHash));
        }

        private static void AddAccountParameters(SqliteCommand command, PlayerAccount account)
        {
            command.Parameters.AddWithValue("$accountId", account.AccountId);
            command.Parameters.AddWithValue("$userName", account.UserName);
            command.Parameters.AddWithValue("$passwordAlgorithm", account.PasswordAlgorithm);
            command.Parameters.AddWithValue("$passwordHash", account.PasswordHash);
            command.Parameters.AddWithValue("$passwordSalt", account.PasswordSalt);
            command.Parameters.AddWithValue("$passwordIterations", account.PasswordIterations);
            command.Parameters.AddWithValue("$chips", (long)account.Chips);
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
            uint balanceBefore,
            uint balanceAfter,
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
                command.Parameters.AddWithValue("$balanceBefore", (long)balanceBefore);
                command.Parameters.AddWithValue("$balanceAfter", (long)balanceAfter);
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
                Chips = checked((uint)reader.GetInt64(6)),
                LastRoomId = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                Version = reader.GetInt64(8),
                CreatedAtUtc = ParseDateTime(reader.GetString(9)),
                UpdatedAtUtc = ParseDateTime(reader.GetString(10)),
                EquippedNamecardId = reader.GetString(11),
            };
        }

        private void RecoverAbandonedHands()
        {
            lock (_syncRoot)
            {
                using (var connection = OpenConnection())
                using (var transaction = connection.BeginTransaction())
                {
                    var handIds = new List<string>();
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "SELECT hand_id FROM poker_hands WHERE status = 'Active';";
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                                handIds.Add(reader.GetString(0));
                        }
                    }

                    var now = DateTime.UtcNow;
                    foreach (var handId in handIds)
                        AbortStoredHand(connection, transaction, handId, "ServerRestart", now);

                    transaction.Commit();
                }
            }
        }

        private static string GetHandStatus(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string handId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT status FROM poker_hands WHERE hand_id = $handId;";
                command.Parameters.AddWithValue("$handId", handId);
                return command.ExecuteScalar() as string;
            }
        }

        private static int GetHandRoomId(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string handId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT room_id FROM poker_hands WHERE hand_id = $handId;";
                command.Parameters.AddWithValue("$handId", handId);
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }



        private static bool IsAccountInActiveHand(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string accountId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT 1
FROM poker_hand_participants AS participant
INNER JOIN poker_hands AS hand ON hand.hand_id = participant.hand_id
WHERE participant.account_id = $accountId AND hand.status = 'Active'
LIMIT 1;";
                command.Parameters.AddWithValue("$accountId", accountId);
                return command.ExecuteScalar() != null;
            }
        }



        private static bool HandRequestExists(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string requestId)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT 1 FROM hand_requests WHERE request_id = $requestId LIMIT 1;";
                command.Parameters.AddWithValue("$requestId", requestId);
                return command.ExecuteScalar() != null;
            }
        }

        private static void InsertHandRequest(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string requestId,
            string handId,
            string requestType,
            DateTime createdAtUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO hand_requests (request_id, hand_id, request_type, created_at_utc)
VALUES ($requestId, $handId, $requestType, $createdAtUtc);";
                command.Parameters.AddWithValue("$requestId", requestId);
                command.Parameters.AddWithValue("$handId", handId);
                command.Parameters.AddWithValue("$requestType", requestType);
                command.Parameters.AddWithValue("$createdAtUtc", FormatDateTime(createdAtUtc));
                command.ExecuteNonQuery();
            }
        }

        private static void UpdateAccountBalance(
            SqliteConnection connection,
            SqliteTransaction transaction,
            PlayerAccount account,
            uint newBalance,
            DateTime updatedAtUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE player_accounts
SET chips = $chips, version = version + 1, updated_at_utc = $updatedAtUtc
WHERE account_id = $accountId AND version = $version;";
                command.Parameters.AddWithValue("$chips", (long)newBalance);
                command.Parameters.AddWithValue("$updatedAtUtc", FormatDateTime(updatedAtUtc));
                command.Parameters.AddWithValue("$accountId", account.AccountId);
                command.Parameters.AddWithValue("$version", account.Version);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("The account changed during a hand transaction.");
            }
        }





        private static void MarkHandCompleted(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string handId,
            string status,
            DateTime completedAtUtc)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE poker_hands SET status = $status, completed_at_utc = $completedAtUtc
WHERE hand_id = $handId AND status = 'Active';";
                command.Parameters.AddWithValue("$status", status);
                command.Parameters.AddWithValue("$completedAtUtc", FormatDateTime(completedAtUtc));
                command.Parameters.AddWithValue("$handId", handId);
                if (command.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("The active hand could not be completed.");
            }
        }

        private static void AbortStoredHand(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string handId,
            string reason,
            DateTime now)
        {
            if (GetHandAccountingMode(connection, transaction, handId) == 1)
            {
                MarkHandCompleted(connection, transaction, handId, "Aborted", now);
                return;
            }
            // Legacy hands debited each wager and still need exactly one recovery refund.
            var participants = new List<(string AccountId, uint Contribution)>();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT account_id, contributed_chips FROM poker_hand_participants
WHERE hand_id = $handId AND contributed_chips > 0;";
                command.Parameters.AddWithValue("$handId", handId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        participants.Add((reader.GetString(0), checked((uint)reader.GetInt64(1))));
                }
            }

            var roomId = GetHandRoomId(connection, transaction, handId);
            foreach (var participant in participants)
            {
                var account = FindById(connection, transaction, participant.AccountId);
                var newBalance = checked(account.Chips + participant.Contribution);
                UpdateAccountBalance(connection, transaction, account, newBalance, now);
                InsertLedger(
                    connection,
                    transaction,
                    Guid.NewGuid().ToString("N"),
                    "abort:" + handId + ":" + participant.AccountId,
                    participant.AccountId,
                    reason == "ServerRestart" ? "CrashRecoveryRefund" : "HandAbortRefund",
                    (long)participant.Contribution,
                    account.Chips,
                    newBalance,
                    roomId,
                    handId,
                    now);
            }

            MarkHandCompleted(connection, transaction, handId, "Aborted", now);
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
       password_iterations, chips, last_room_id, version, created_at_utc, updated_at_utc, equipped_namecard
FROM player_accounts";
    }
}
#endif
