#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Game.Achievements;
using Game.Events;
using Game.Tasks;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : IAchievementRepository, IPlayerEventRepository
    {
        private static void CreateAchievementSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS achievement_accounts(account_id TEXT PRIMARY KEY REFERENCES player_accounts(account_id), document TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS achievement_events(sequence INTEGER PRIMARY KEY AUTOINCREMENT, event_id TEXT NOT NULL UNIQUE, hand_id TEXT NOT NULL, payload TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS achievement_event_receipts(event_id TEXT NOT NULL REFERENCES achievement_events(event_id), account_id TEXT NOT NULL REFERENCES player_accounts(account_id), consumed INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(event_id, account_id));
CREATE INDEX IF NOT EXISTS ix_achievement_pending ON achievement_event_receipts(consumed, event_id);";
            command.ExecuteNonQuery();
        }

        public void InitializeAchievements()
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                CreateAchievementSchema(connection, transaction);
                CreatePlayerEventSchema(connection, transaction);
                transaction.Commit();
            }
        }

        public void PublishAchievementEvent(AchievementContext context)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                InsertAchievementEvent(connection, transaction, context);
                transaction.Commit();
            }
        }

        private static void InsertAchievementEvent(SqliteConnection connection, SqliteTransaction transaction, AchievementContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.HandId) || context.EventId != context.HandId + ":achievement:" + (int)context.Stage ||
                context.Streets.Count == 0 || context.Streets[context.Streets.Count - 1].Stage != context.Stage ||
                context.Players.Count < 2 || context.Players.Any(p => string.IsNullOrWhiteSpace(p.AccountId)) ||
                context.Players.Select(p => p.AccountId).Distinct().Count() != context.Players.Count)
                throw new InvalidOperationException("Invalid achievement event identity or participants.");
            InsertPlayerEvent(connection, transaction, new HandStageRecordedEvent(context));
        }

        public PendingAchievementEvent[] ReadPendingAchievementEvents(int limit)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT r.account_id,e.payload,e.source_id,e.event_type,e.event_version,e.event_id FROM achievement_events e
JOIN achievement_event_receipts r ON r.event_id=e.event_id WHERE r.consumed=0 ORDER BY e.sequence,r.account_id LIMIT $limit;";
                command.Parameters.AddWithValue("$limit", Math.Max(1, Math.Min(1000, limit)));
                using var reader = command.ExecuteReader();
                var pending = new List<PendingAchievementEvent>();
                while (reader.Read())
                {
                    var playerEvent = EventRegistry.Read(reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetString(1));
                    if (playerEvent.EventId != reader.GetString(5))
                        throw new InvalidOperationException("Player event payload identity mismatch.");
                    pending.Add(new PendingAchievementEvent { AccountId = reader.GetString(0), Event = playerEvent });
                }
                return pending.ToArray();
            }
        }

        public T TransactAchievements<T>(string accountId, Func<AchievementAccountTransaction, T> operation, string eventId = null)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                var account = FindById(connection, transaction, accountId) ?? throw new InvalidOperationException("Achievement account missing.");
                var document = new AchievementDocument();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.Parameters.AddWithValue("$account", accountId);
                command.CommandText = "SELECT document FROM achievement_accounts WHERE account_id=$account;";
                if (command.ExecuteScalar() is string json)
                    document = JsonSerializer.Deserialize<AchievementDocument>(json) ?? throw new InvalidOperationException("Invalid achievement document.");
                var consumed = false;
                if (eventId != null)
                {
                    command.Parameters.AddWithValue("$event", eventId);
                    command.CommandText = "SELECT consumed FROM achievement_event_receipts WHERE event_id=$event AND account_id=$account;";
                    var value = command.ExecuteScalar();
                    if (value == null) throw new InvalidOperationException("Achievement event does not belong to this account.");
                    consumed = Convert.ToInt64(value) != 0;
                }
                var tx = new SqliteAchievementTransaction(connection, transaction, account, document, consumed);
                var result = operation(tx);
                if (tx.Changed)
                {
                    document.Revision = checked(document.Revision + 1);
                    command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(document));
                    command.CommandText = @"INSERT INTO achievement_accounts(account_id,document) VALUES($account,$json)
ON CONFLICT(account_id) DO UPDATE SET document=excluded.document;";
                    command.ExecuteNonQuery();
                }
                if (eventId != null && !consumed)
                {
                    command.CommandText = "UPDATE achievement_event_receipts SET consumed=1 WHERE event_id=$event AND account_id=$account;";
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
                return result;
            }
        }

        private sealed class SqliteAchievementTransaction : AchievementAccountTransaction
        {
            private readonly SqliteConnection _connection;
            private readonly SqliteTransaction _transaction;
            public override bool CanClaim => true;

            public SqliteAchievementTransaction(SqliteConnection connection, SqliteTransaction transaction,
                PlayerAccount account, AchievementDocument document, bool consumed)
            {
                _connection = connection; _transaction = transaction; Account = account; Document = document; AlreadyConsumed = consumed;
            }

            public override AchievementResult Grant(Reward reward, IReadOnlyDictionary<string, uint> limits, string claimKey)
            {
                if (!CanClaim) return AchievementResult.OutsideRoomRequired;
                if ((ulong)Account.Dollars + reward.Chip > uint.MaxValue) return AchievementResult.BalanceLimit;
                var owned = ReadItemStacks(_connection, _transaction, Account.AccountId).ToDictionary(i => i.ItemId, i => i.Quantity);
                foreach (var item in reward.Items)
                {
                    if (NamecardItemId.TryToNamecardId(item.ItemId, out _))
                    {
                        // Permanent cosmetics are idempotent. Existing ownership must not block the dollar reward.
                        if (!limits.TryGetValue(item.ItemId, out var namecardLimit) || namecardLimit != 1 || item.Quantity != 1)
                            return AchievementResult.ItemLimit;
                        continue;
                    }
                    owned.TryGetValue(item.ItemId, out var count);
                    if (!limits.TryGetValue(item.ItemId, out var maximum) || (ulong)count + item.Quantity > maximum)
                        return AchievementResult.ItemLimit;
                }
                // Reward.Chip is retained in old JSON snapshots; redemptions now always credit dollars.
                var before = Account.Dollars;
                var after = checked(before + reward.Chip);
                using (var update = _connection.CreateCommand())
                {
                    update.Transaction = _transaction;
                    update.CommandText = @"UPDATE player_accounts SET dollars=$chips,version=version+1,updated_at_utc=$now
WHERE account_id=$account AND version=$version;";
                    update.Parameters.AddWithValue("$chips", (long)after);
                    update.Parameters.AddWithValue("$now", FormatDateTime(DateTime.UtcNow));
                    update.Parameters.AddWithValue("$account", Account.AccountId);
                    update.Parameters.AddWithValue("$version", Account.Version);
                    if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Achievement reward account version changed.");
                }
                foreach (var item in reward.Items)
                {
                    using var update = _connection.CreateCommand();
                    update.Transaction = _transaction;
                    update.Parameters.AddWithValue("$account", Account.AccountId);
                    if (NamecardItemId.TryToNamecardId(item.ItemId, out var namecardId))
                    {
                        // Use the shop's ownership table in the same transaction as dollars and claim state.
                        update.CommandText = @"INSERT INTO account_namecards(account_id,product_id,purchased_at_utc)
VALUES($account,$product,$now) ON CONFLICT(account_id,product_id) DO NOTHING;";
                        update.Parameters.AddWithValue("$product", namecardId);
                        update.Parameters.AddWithValue("$now", FormatDateTime(DateTime.UtcNow));
                    }
                    else
                    {
                        update.CommandText = @"INSERT INTO account_items(account_id,item_id,quantity) VALUES($account,$item,$count)
ON CONFLICT(account_id,item_id) DO UPDATE SET quantity=quantity+excluded.quantity;";
                        update.Parameters.AddWithValue("$item", item.ItemId);
                        update.Parameters.AddWithValue("$count", (long)item.Quantity);
                    }
                    update.ExecuteNonQuery();
                }
                if (reward.Items.Any(item => !NamecardItemId.TryToNamecardId(item.ItemId, out _)))
                    RecordInventoryChanged(_connection, _transaction, Account.AccountId, claimKey, "achievement-reward", DateTime.UtcNow);
                InsertDollarLedger(_connection, _transaction, Guid.NewGuid().ToString("N"), claimKey, Account.AccountId,
                    "AchievementReward", reward.Chip, before, after, null, null, DateTime.UtcNow);
                Account.Dollars = after;
                Account.Version++;
                Changed = true;
                return AchievementResult.Success;
            }
        }
    }
}
#endif
