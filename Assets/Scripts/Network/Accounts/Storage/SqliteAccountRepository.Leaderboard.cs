#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : IServiceYearsRepository
    {
        // The account-column expression is supplied only by the SQL statements below.
        private static string ServiceYearsSql(string accountColumn) =>
            "(SELECT COALESCE(SUM(quantity * CASE item_id " +
            "WHEN 'devil-contract' THEN 1 WHEN 'archdevil-contract' THEN 10 " +
            "WHEN 'satan-contract' THEN 100 ELSE 0 END), 0) FROM account_items WHERE account_id = " +
            accountColumn + ")";

        public Dictionary<string, ulong> ReadServiceYears(string[] accountIds)
        {
            var result = new Dictionary<string, ulong>(StringComparer.Ordinal);
            var ids = (accountIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
            if (ids.Length == 0) return result;
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                var parameters = ids.Select((id, index) => "$id" + index).ToArray();
                for (int i = 0; i < ids.Length; i++)
                    command.Parameters.AddWithValue(parameters[i], ids[i]);
                command.CommandText = "SELECT account_id, " + ServiceYearsSql("player_accounts.account_id") +
                    " FROM player_accounts WHERE account_id IN (" + string.Join(",", parameters) + ");";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    result.Add(reader.GetString(0), checked((ulong)reader.GetInt64(1)));
            }
            return result;
        }

        private static (ServiceLeaderboardEntry[] TopPlayers, ServiceLeaderboardEntry Self) ReadServiceLeaderboard(
            SqliteConnection connection, SqliteTransaction transaction, string accountId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            // LEFT JOIN keeps accounts without contracts (including offline accounts) in the ranking.
            // SQLite INTEGER and the projection's ulong arithmetic both accommodate three uint-sized stacks.
            command.CommandText = @"
WITH totals AS (
    SELECT a.account_id, a.user_name, a.created_at_utc,
           COALESCE(d.quantity, 0) AS devil,
           COALESCE(g.quantity, 0) AS archdevil,
           COALESCE(s.quantity, 0) AS satan
    FROM player_accounts AS a
    LEFT JOIN account_items AS d ON d.account_id = a.account_id AND d.item_id = 'devil-contract'
    LEFT JOIN account_items AS g ON g.account_id = a.account_id AND g.item_id = 'archdevil-contract'
    LEFT JOIN account_items AS s ON s.account_id = a.account_id AND s.item_id = 'satan-contract'
), ranked AS (
    SELECT *, ROW_NUMBER() OVER (
        ORDER BY devil + 10 * archdevil + 100 * satan DESC,
                 created_at_utc ASC, account_id COLLATE BINARY ASC) AS position
    FROM totals
)
SELECT account_id, user_name, devil, archdevil, satan, position
FROM ranked WHERE position <= 10 OR account_id = $accountId ORDER BY position;";
            command.Parameters.AddWithValue("$accountId", accountId);
            using var reader = command.ExecuteReader();
            var top = new List<ServiceLeaderboardEntry>(10);
            var self = default(ServiceLeaderboardEntry);
            while (reader.Read())
            {
                var entry = new ServiceLeaderboardEntry
                {
                    UserName = reader.GetString(1),
                    DevilContracts = checked((uint)reader.GetInt64(2)),
                    ArchdevilContracts = checked((uint)reader.GetInt64(3)),
                    SatanContracts = checked((uint)reader.GetInt64(4)),
                    Rank = reader.GetInt64(5), IsSelf = reader.GetString(0) == accountId,
                };
                if (entry.Rank <= 10) top.Add(entry);
                if (entry.IsSelf) self = entry;
            }
            if (!self.IsSelf)
                throw new InvalidOperationException("The authenticated account no longer exists.");
            return (top.ToArray(), self);
        }
    }
}
#endif
