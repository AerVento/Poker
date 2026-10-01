#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Events;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository
    {
        private static readonly PlayerEventRegistry EventRegistry = PlayerEventRegistry.CreateDefault();

        private static void CreatePlayerEventSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            var columns = new HashSet<string>(StringComparer.Ordinal);
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "PRAGMA table_info(achievement_events);";
                using var reader = command.ExecuteReader();
                while (reader.Read()) columns.Add(reader.GetString(1));
            }
            foreach (var column in new[]
            {
                (Name: "source_id", Sql: "TEXT NOT NULL DEFAULT 'table'"),
                (Name: "event_type", Sql: "TEXT NOT NULL DEFAULT 'stage'"),
                (Name: "event_version", Sql: "INTEGER NOT NULL DEFAULT 1")
            })
            {
                if (columns.Contains(column.Name)) continue;
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "ALTER TABLE achievement_events ADD COLUMN " + column.Name + " " + column.Sql + ";";
                command.ExecuteNonQuery();
            }
        }

        public void PublishPlayerEvent(PlayerEvent playerEvent)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                InsertPlayerEvent(connection, transaction, playerEvent);
                transaction.Commit();
            }
        }

        private sealed class TransactionEventWriter : IPlayerEventWriter
        {
            private readonly SqliteConnection _connection;
            private readonly SqliteTransaction _transaction;

            public TransactionEventWriter(SqliteConnection connection, SqliteTransaction transaction)
            {
                _connection = connection;
                _transaction = transaction;
            }

            public void Append(PlayerEvent playerEvent) => InsertPlayerEvent(_connection, _transaction, playerEvent);
        }

        private static void InsertPlayerEvent(SqliteConnection connection, SqliteTransaction transaction, PlayerEvent playerEvent)
        {
            if (playerEvent == null || string.IsNullOrWhiteSpace(playerEvent.EventId) || playerEvent.OccurredAt <= 0 ||
                playerEvent.AccountIds == null || playerEvent.AccountIds.Count == 0 ||
                playerEvent.AccountIds.Any(string.IsNullOrWhiteSpace) ||
                playerEvent.AccountIds.Distinct(StringComparer.Ordinal).Count() != playerEvent.AccountIds.Count)
                throw new InvalidOperationException("Invalid player event identity or recipients.");
            var codec = EventRegistry.For(playerEvent);
            var payload = codec.Encode(playerEvent);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            // hand_id is retained for legacy queries; non-table events deliberately have no hand.
            command.CommandText = @"INSERT OR IGNORE INTO achievement_events
(event_id,hand_id,payload,source_id,event_type,event_version) VALUES($event,$hand,$payload,$source,$type,$version);";
            command.Parameters.AddWithValue("$event", playerEvent.EventId);
            command.Parameters.AddWithValue("$hand", (playerEvent as HandStageRecordedEvent)?.Context.HandId ??
                (playerEvent as HandActionRecordedEvent)?.HandId ??
                (playerEvent as HandCardsRevealedEvent)?.HandId ?? "");
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$source", codec.SourceId);
            command.Parameters.AddWithValue("$type", codec.TypeId);
            command.Parameters.AddWithValue("$version", codec.Version);
            command.ExecuteNonQuery();
            command.CommandText = "SELECT payload,source_id,event_type,event_version FROM achievement_events WHERE event_id=$event;";
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read() || reader.GetString(0) != payload || reader.GetString(1) != codec.SourceId ||
                    reader.GetString(2) != codec.TypeId || reader.GetInt32(3) != codec.Version)
                    throw new InvalidOperationException("Player event ID reused with different facts.");
            }
            command.CommandText = "INSERT OR IGNORE INTO achievement_event_receipts(event_id,account_id) VALUES($event,$account);";
            var account = command.Parameters.Add("$account", SqliteType.Text);
            foreach (var recipient in playerEvent.AccountIds)
            {
                account.Value = recipient;
                command.ExecuteNonQuery();
            }
        }

        private static void RecordInventoryChanged(SqliteConnection connection, SqliteTransaction transaction,
            string accountId, string operationId, string reason, DateTime now)
        {
            InventoryEventSource.Changed(new TransactionEventWriter(connection, transaction), accountId, operationId,
                new DateTimeOffset(now).ToUnixTimeMilliseconds(), reason, ReadItemStacks(connection, transaction, accountId));
            RecordAssetsChanged(connection, transaction, accountId, operationId, reason, now);
        }

        private static void RecordAssetsChanged(SqliteConnection connection, SqliteTransaction transaction,
            string accountId, string operationId, string reason, DateTime now)
        {
            var account = FindById(connection, transaction, accountId);
            if (account == null) throw new InvalidOperationException("Cannot capture assets for a missing account.");
            AccountEventSource.AssetsChanged(new TransactionEventWriter(connection, transaction), accountId, operationId,
                new DateTimeOffset(now).ToUnixTimeMilliseconds(), reason, account.Dollars, account.Chips,
                ReadItemStacks(connection, transaction, accountId));
        }
    }
}
#endif
