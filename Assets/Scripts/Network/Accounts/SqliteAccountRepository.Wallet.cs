#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : IWalletRepository, IWalletPaymentRepository
    {
        private static void CreateWalletSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using (var alter = connection.CreateCommand())
            {
                alter.Transaction = transaction;
                alter.CommandText = "ALTER TABLE player_accounts ADD COLUMN dollars INTEGER NOT NULL DEFAULT 0 CHECK(dollars BETWEEN 0 AND 4294967295);";
                alter.ExecuteNonQuery();
            }
            // Recover pre-upgrade escrow before converting the complete old balance exactly once.
            var active = new System.Collections.Generic.List<string>();
            using (var query = connection.CreateCommand())
            {
                query.Transaction = transaction;
                query.CommandText = "SELECT hand_id FROM poker_hands WHERE status='Active';";
                using var reader = query.ExecuteReader();
                while (reader.Read()) active.Add(reader.GetString(0));
            }
            foreach (var handId in active) AbortStoredHand(connection, transaction, handId, "ServerRestart", DateTime.UtcNow);
            foreach (var sql in new[]
            {
                "ALTER TABLE poker_hand_participants ADD COLUMN wallet_committed INTEGER NOT NULL DEFAULT 0 CHECK(wallet_committed BETWEEN 0 AND 4294967295);",
                "ALTER TABLE poker_hand_participants ADD COLUMN wallet_exchange_allowed INTEGER NOT NULL DEFAULT 0;",
                @"CREATE TABLE dollar_ledger (
transaction_id TEXT NOT NULL PRIMARY KEY, request_id TEXT NOT NULL UNIQUE,
account_id TEXT NOT NULL REFERENCES player_accounts(account_id), room_id INTEGER NULL, hand_id TEXT NULL,
transaction_type TEXT NOT NULL, amount INTEGER NOT NULL,
balance_before INTEGER NOT NULL CHECK(balance_before BETWEEN 0 AND 4294967295),
balance_after INTEGER NOT NULL CHECK(balance_after BETWEEN 0 AND 4294967295), created_at_utc TEXT NOT NULL);",
                @"CREATE TABLE wallet_requests (account_id TEXT NOT NULL REFERENCES player_accounts(account_id),
operation_id TEXT NOT NULL, fingerprint TEXT NOT NULL, PRIMARY KEY(account_id,operation_id));",
                // Historical chip ledger/events remain immutable. Explicit transfer legs establish both balances.
                @"INSERT INTO dollar_ledger SELECT 'migration-dollar:'||account_id,'migration-dollar:'||account_id,
account_id,NULL,NULL,'LegacyBalanceMigration',chips,0,chips,updated_at_utc FROM player_accounts;",
                @"INSERT INTO chip_ledger(transaction_id,request_id,account_id,room_id,hand_id,transaction_type,
amount,balance_before,balance_after,created_at_utc) SELECT 'migration-chip:'||account_id,'migration-chip:'||account_id,
account_id,NULL,NULL,'LegacyBalanceMigration',-chips,chips,0,updated_at_utc FROM player_accounts;",
                "UPDATE player_accounts SET dollars=chips,chips=0,version=version+1;",
                "CREATE INDEX ix_account_assets ON player_accounts((chips+dollars) DESC,created_at_utc,account_id);"
            })
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        public void SetHandWalletAccess(string handId, string accountId, uint committedChips, bool canExchange)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = @"UPDATE poker_hand_participants SET wallet_committed=$committed,
wallet_exchange_allowed=$allowed WHERE hand_id=$hand AND account_id=$account
AND EXISTS(SELECT 1 FROM poker_hands WHERE hand_id=$hand AND status='Active');";
                command.Parameters.AddWithValue("$committed", (long)committedChips);
                command.Parameters.AddWithValue("$allowed", canExchange ? 1 : 0);
                command.Parameters.AddWithValue("$hand", handId);
                command.Parameters.AddWithValue("$account", accountId);
                command.ExecuteNonQuery();
            }
        }

        private static WalletSnapshot ReadWallet(SqliteConnection connection, SqliteTransaction transaction, PlayerAccount account)
        {
            if (account == null) return null;
            var snapshot = new WalletSnapshot { Account = account, CanExchange = true, AvailableChips = account.Chips };
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"SELECT p.wallet_committed,p.wallet_exchange_allowed FROM poker_hand_participants p
JOIN poker_hands h ON h.hand_id=p.hand_id WHERE p.account_id=$account AND h.status='Active';";
            command.Parameters.AddWithValue("$account", account.AccountId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var committed = checked((uint)reader.GetInt64(0));
                snapshot.AvailableChips = committed <= snapshot.AvailableChips ? snapshot.AvailableChips - committed : 0;
                snapshot.CanExchange &= reader.GetInt32(1) == 1;
            }
            return snapshot;
        }

        public WalletSnapshot ReadWallet(string accountId)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction(deferred: true);
                var result = ReadWallet(connection, transaction, FindById(connection, transaction, accountId));
                transaction.Commit();
                return result;
            }
        }

        public WalletResult ExchangeWallet(string accountId, string operationId, bool dollarsToChips, uint amount)
        {
            if (!Guid.TryParseExact(operationId, "N", out _) || amount == 0) return WalletResult.InvalidRequest;
            var fingerprint = (dollarsToChips ? "buy:" : "sell:") + amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                using (var prior = connection.CreateCommand())
                {
                    prior.Transaction = transaction;
                    prior.CommandText = "SELECT fingerprint FROM wallet_requests WHERE account_id=$account AND operation_id=$operation;";
                    prior.Parameters.AddWithValue("$account", accountId);
                    prior.Parameters.AddWithValue("$operation", operationId);
                    var saved = prior.ExecuteScalar() as string;
                    if (saved != null) return saved == fingerprint ? WalletResult.AlreadyApplied : WalletResult.InvalidRequest;
                }
                var account = FindById(connection, transaction, accountId);
                if (account == null) return WalletResult.Unavailable;
                var result = ConvertWallet(connection, transaction, account, dollarsToChips, amount, "exchange:" + accountId + ":" + operationId);
                if (result != WalletResult.Success) return result;
                using var receipt = connection.CreateCommand();
                receipt.Transaction = transaction;
                receipt.CommandText = "INSERT INTO wallet_requests(account_id,operation_id,fingerprint) VALUES($account,$operation,$fingerprint);";
                receipt.Parameters.AddWithValue("$account", accountId);
                receipt.Parameters.AddWithValue("$operation", operationId);
                receipt.Parameters.AddWithValue("$fingerprint", fingerprint);
                receipt.ExecuteNonQuery();
                transaction.Commit();
                return WalletResult.Success;
            }
        }

        private static WalletResult ConvertWallet(SqliteConnection connection, SqliteTransaction transaction,
            PlayerAccount account, bool dollarsToChips, uint amount, string operation)
        {
            var wallet = ReadWallet(connection, transaction, account);
            if (!wallet.CanExchange) return WalletResult.ExchangeLocked;
            if (amount > (dollarsToChips ? account.Dollars : wallet.AvailableChips)) return WalletResult.InsufficientFunds;
            if ((ulong)(dollarsToChips ? account.Chips : account.Dollars) + amount > uint.MaxValue) return WalletResult.BalanceLimit;
            var dollars = dollarsToChips ? account.Dollars - amount : account.Dollars + amount;
            var chips = dollarsToChips ? account.Chips + amount : account.Chips - amount;
            var now = DateTime.UtcNow;
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = @"UPDATE player_accounts SET dollars=$dollars,chips=$chips,version=version+1,updated_at_utc=$now
WHERE account_id=$account AND version=$version;";
            update.Parameters.AddWithValue("$account", account.AccountId);
            update.Parameters.AddWithValue("$version", account.Version);
            update.Parameters.AddWithValue("$chips", (long)chips);
            update.Parameters.AddWithValue("$dollars", (long)dollars);
            update.Parameters.AddWithValue("$now", FormatDateTime(now));
            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Account changed during wallet exchange.");
            InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"), operation + ":dollars", account.AccountId,
                "CurrencyExchange", (long)dollars - account.Dollars, account.Dollars, dollars, null, null, now);
            InsertLedger(connection, transaction, Guid.NewGuid().ToString("N"), operation + ":chips", account.AccountId,
                "CurrencyExchange", (long)chips - account.Chips, account.Chips, chips, null, null, now);
            account.Dollars = dollars; account.Chips = chips; account.Version++; account.UpdatedAtUtc = now;
            return WalletResult.Success;
        }

        private static WalletResult PrepareDollarPayment(SqliteConnection connection, SqliteTransaction transaction,
            PlayerAccount account, ulong cost, bool authorizeChipConversion, uint authorizedShortfall, string operation)
        {
            if (cost > uint.MaxValue) return WalletResult.BalanceLimit;
            var shortfall = cost > account.Dollars ? (uint)(cost - account.Dollars) : 0;
            if (authorizeChipConversion && shortfall != authorizedShortfall) return WalletResult.QuoteChanged;
            if (shortfall == 0) return WalletResult.Success;
            var wallet = ReadWallet(connection, transaction, account);
            if (!wallet.CanExchange || wallet.AvailableChips < shortfall) return WalletResult.InsufficientFunds;
            if (!authorizeChipConversion) return WalletResult.ConversionRequired;
            return ConvertWallet(connection, transaction, account, false, shortfall, "payment:" + account.AccountId + ":" + operation);
        }

        public ChipMutationResult ApplyDollarMutation(string accountId, string requestId, string transactionType,
            long amount, uint? maximumBalanceInclusive, out PlayerAccount account)
        {
            account = null;
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(requestId) || string.IsNullOrEmpty(transactionType))
                throw new ArgumentException("A monetary mutation requires stable identity.");
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                account = FindById(connection, transaction, accountId);
                if (account == null) return ChipMutationResult.AccountNotFound;
                // Old claims stay consumed after the one-time currency migration.
                using (var prior = connection.CreateCommand())
                {
                    prior.Transaction = transaction;
                    prior.CommandText = @"SELECT account_id,transaction_type,amount FROM dollar_ledger WHERE request_id=$request
UNION ALL SELECT account_id,transaction_type,amount FROM chip_ledger WHERE request_id=$request LIMIT 1;";
                    prior.Parameters.AddWithValue("$request", requestId);
                    using var reader = prior.ExecuteReader();
                    if (reader.Read())
                    {
                        if (reader.GetString(0) != accountId || reader.GetString(1) != transactionType || reader.GetInt64(2) != amount)
                            return ChipMutationResult.BalanceRequirementNotMet;
                        return ChipMutationResult.AlreadyApplied;
                    }
                }
                if (maximumBalanceInclusive.HasValue && account.TotalAssets > maximumBalanceInclusive.Value)
                    return ChipMutationResult.BalanceRequirementNotMet;
                if (amount < -(long)account.Dollars) return ChipMutationResult.InsufficientChips;
                if (amount > (long)uint.MaxValue - account.Dollars) return ChipMutationResult.Overflow;
                var next = (uint)(account.Dollars + amount);
                var now = DateTime.UtcNow;
                UpdateDollarBalance(connection, transaction, account, next, now);
                InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"), requestId, accountId,
                    transactionType, amount, account.Dollars, next, null, null, now);
                transaction.Commit();
                account.Dollars = next; account.Version++; account.UpdatedAtUtc = now;
                return ChipMutationResult.Applied;
            }
        }

        private static void UpdateDollarBalance(SqliteConnection connection, SqliteTransaction transaction,
            PlayerAccount account, uint dollars, DateTime now)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE player_accounts SET dollars=$dollars,version=version+1,updated_at_utc=$now WHERE account_id=$account AND version=$version;";
            command.Parameters.AddWithValue("$dollars", (long)dollars);
            command.Parameters.AddWithValue("$account", account.AccountId);
            command.Parameters.AddWithValue("$version", account.Version);
            command.Parameters.AddWithValue("$now", FormatDateTime(now));
            if (command.ExecuteNonQuery() != 1) throw new InvalidOperationException("Account changed during dollar transaction.");
        }

        private static void InsertDollarLedger(SqliteConnection connection, SqliteTransaction transaction,
            string transactionId, string requestId, string accountId, string transactionType, long amount,
            uint balanceBefore, uint balanceAfter, int? roomId, string handId, DateTime createdAtUtc)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"INSERT INTO dollar_ledger(transaction_id,request_id,account_id,room_id,hand_id,
transaction_type,amount,balance_before,balance_after,created_at_utc)
VALUES($id,$request,$account,$room,$hand,$type,$amount,$before,$after,$now);";
            command.Parameters.AddWithValue("$id", transactionId);
            command.Parameters.AddWithValue("$request", requestId);
            command.Parameters.AddWithValue("$account", accountId);
            command.Parameters.AddWithValue("$room", roomId.HasValue ? (object)roomId.Value : DBNull.Value);
            command.Parameters.AddWithValue("$hand", (object)handId ?? DBNull.Value);
            command.Parameters.AddWithValue("$type", transactionType);
            command.Parameters.AddWithValue("$amount", amount);
            command.Parameters.AddWithValue("$before", (long)balanceBefore);
            command.Parameters.AddWithValue("$after", (long)balanceAfter);
            command.Parameters.AddWithValue("$now", FormatDateTime(createdAtUtc));
            command.ExecuteNonQuery();
        }
    }
}
#endif
