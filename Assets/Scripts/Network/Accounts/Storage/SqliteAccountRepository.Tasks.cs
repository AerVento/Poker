#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Game.Facts;
using Game.Tasks;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : ITaskRepository
    {
        private static void CreateTaskSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS task_accounts(account_id TEXT PRIMARY KEY REFERENCES player_accounts(account_id), document TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS game_facts(sequence INTEGER PRIMARY KEY AUTOINCREMENT,event_id TEXT NOT NULL UNIQUE,account_id TEXT NOT NULL,hand_id TEXT NOT NULL,payload TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS task_event_receipts(event_id TEXT PRIMARY KEY,account_id TEXT NOT NULL,consumed_at TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS ix_game_facts_hand ON game_facts(hand_id);";
            command.ExecuteNonQuery();
        }

        public void InitializeTasks()
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                CreateTaskSchema(connection, transaction);
                transaction.Commit();
            }
        }

        public string[] GetTaskAccountIds()
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT account_id FROM player_accounts ORDER BY account_id;";
                var ids = new List<string>();
                using var reader = command.ExecuteReader();
                while (reader.Read()) ids.Add(reader.GetString(0));
                return ids.ToArray();
            }
        }

        public T TransactTasks<T>(string accountId, Func<TaskAccountTransaction, T> operation, string eventId = null)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                var account = FindById(connection, transaction, accountId)
                    ?? throw new InvalidOperationException("Task account does not exist.");
                var document = new PlayerTaskDocument();
                using (var read = connection.CreateCommand())
                {
                    read.Transaction = transaction;
                    read.CommandText = "SELECT document FROM task_accounts WHERE account_id=$account;";
                    read.Parameters.AddWithValue("$account", accountId);
                    var json = read.ExecuteScalar() as string;
                    if (json != null) document = JsonSerializer.Deserialize<PlayerTaskDocument>(json)
                        ?? throw new InvalidOperationException("Invalid task account document.");
                }
                var consumed = false;
                if (eventId != null)
                {
                    using var check = connection.CreateCommand();
                    check.Transaction = transaction;
                    check.CommandText = "SELECT account_id FROM task_event_receipts WHERE event_id=$event;";
                    check.Parameters.AddWithValue("$event", eventId);
                    var owner = check.ExecuteScalar() as string;
                    if (owner != null && owner != accountId) throw new InvalidOperationException("Event account mismatch.");
                    consumed = owner != null;
                }
                var context = new SqliteTaskTransaction(connection, transaction, account, document, consumed);
                var result = operation(context);
                if (context.Changed)
                {
                    document.Revision = checked(document.Revision + 1);
                    using var save = connection.CreateCommand();
                    save.Transaction = transaction;
                    save.CommandText = @"INSERT INTO task_accounts(account_id,document) VALUES($account,$json)
ON CONFLICT(account_id) DO UPDATE SET document=excluded.document;";
                    save.Parameters.AddWithValue("$account", accountId);
                    save.Parameters.AddWithValue("$json", JsonSerializer.Serialize(document));
                    save.ExecuteNonQuery();
                }
                if (eventId != null && !consumed)
                {
                    using var mark = connection.CreateCommand();
                    mark.Transaction = transaction;
                    mark.CommandText = "INSERT INTO task_event_receipts VALUES($event,$account,$now);";
                    mark.Parameters.AddWithValue("$event", eventId);
                    mark.Parameters.AddWithValue("$account", accountId);
                    mark.Parameters.AddWithValue("$now", FormatDateTime(DateTime.UtcNow));
                    mark.ExecuteNonQuery();
                }
                transaction.Commit();
                return result;
            }
        }

        public void PublishHandFact(HandFact fact)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                InsertHandFact(connection, transaction, fact);
                transaction.Commit();
            }
        }

        private static void InsertHandFact(SqliteConnection connection, SqliteTransaction transaction, HandFact fact)
        {
            if (fact == null || string.IsNullOrWhiteSpace(fact.EventId) || string.IsNullOrWhiteSpace(fact.AccountId) ||
                string.IsNullOrWhiteSpace(fact.HandId)) throw new ArgumentException("A hand fact requires stable identity.");
            var payload = JsonSerializer.Serialize(fact);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT OR IGNORE INTO game_facts(event_id,account_id,hand_id,payload) VALUES($event,$account,$hand,$payload);";
            command.Parameters.AddWithValue("$event", fact.EventId);
            command.Parameters.AddWithValue("$account", fact.AccountId);
            command.Parameters.AddWithValue("$hand", fact.HandId);
            command.Parameters.AddWithValue("$payload", payload);
            command.ExecuteNonQuery();
            command.CommandText = "SELECT payload FROM game_facts WHERE event_id=$event;";
            if ((string)command.ExecuteScalar() != payload) throw new InvalidOperationException("Hand event ID reused with different facts.");
        }

        public HandFact[] ReadPendingHandFacts(int limit)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT f.payload FROM game_facts f LEFT JOIN task_event_receipts r ON r.event_id=f.event_id
WHERE r.event_id IS NULL ORDER BY f.sequence LIMIT $limit;";
                command.Parameters.AddWithValue("$limit", Math.Max(1, Math.Min(limit, 1000)));
                var facts = new List<HandFact>();
                using var reader = command.ExecuteReader();
                while (reader.Read()) facts.Add(JsonSerializer.Deserialize<HandFact>(reader.GetString(0)));
                return facts.ToArray();
            }
        }

        private sealed class SqliteTaskTransaction : TaskAccountTransaction
        {
            private readonly SqliteConnection _connection;
            private readonly SqliteTransaction _transaction;
            public SqliteTaskTransaction(SqliteConnection connection, SqliteTransaction transaction,
                PlayerAccount account, PlayerTaskDocument document, bool consumed)
            {
                _connection = connection; _transaction = transaction;
                Account = account; Document = document; AlreadyConsumed = consumed;
                HasActiveHand = IsAccountInActiveHand(connection, transaction, account.AccountId);
            }
            public override DailyTaskResult Grant(Reward reward, IReadOnlyDictionary<string, uint> limits, string claimKey)
            {
                var result = ApplyReward(_connection, _transaction, Account, reward, limits, claimKey,
                    "TaskReward", out var granted);
                if (result != RewardGrantStatus.Success)
                    return result == RewardGrantStatus.ActiveHand ? DailyTaskResult.ActiveHand :
                        result == RewardGrantStatus.BalanceLimit ? DailyTaskResult.BalanceLimit : DailyTaskResult.ItemLimit;
                GrantedReward = granted;
                Changed = true;
                return DailyTaskResult.Success;
            }
        }
    }
}
#endif
