#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : IServiceYearsRepository
    {
        // Contract IDs pass GameCatalog.IsValidId (no quotes) and years are integers, so both are safe to inline.
        private string ServiceYearsSql(string accountColumn) => _contracts.Length == 0 ? "0" :
            "(SELECT COALESCE(SUM(quantity * CASE item_id " +
            string.Concat(_contracts.Select(c => "WHEN '" + c.ItemId + "' THEN " + c.ServiceYears.ToString(CultureInfo.InvariantCulture) + " ")) +
            "ELSE 0 END), 0) FROM account_items WHERE account_id = " + accountColumn + ")";

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

        private (ServiceLeaderboardEntry[] TopPlayers, ServiceLeaderboardEntry Self) ReadServiceLeaderboard(
            SqliteConnection connection, SqliteTransaction transaction, string accountId)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var columns = string.Concat(_contracts.Select((c, i) => ", COALESCE(c" + i + ".quantity, 0) AS c" + i));
            var joins = string.Concat(_contracts.Select((c, i) =>
                " LEFT JOIN account_items AS c" + i + " ON c" + i + ".account_id = a.account_id AND c" + i + ".item_id = '" + c.ItemId + "'"));
            var years = _contracts.Length == 0 ? "0" : string.Join(" + ", _contracts.Select((c, i) =>
                "c" + i + " * " + c.ServiceYears.ToString(CultureInfo.InvariantCulture)));
            // LEFT JOIN keeps accounts without contracts (including offline accounts) in the ranking.
            command.CommandText = @"
WITH totals AS (
    SELECT a.account_id, a.user_name, a.created_at_utc" + columns + @"
    FROM player_accounts AS a" + joins + @"
), ranked AS (
    SELECT *, " + years + @" AS years, ROW_NUMBER() OVER (
        ORDER BY " + years + @" DESC, created_at_utc ASC, account_id COLLATE BINARY ASC) AS position
    FROM totals
)
SELECT * FROM ranked WHERE position <= 10 OR account_id = $accountId ORDER BY position;";
            command.Parameters.AddWithValue("$accountId", accountId);
            using var reader = command.ExecuteReader();
            var top = new List<ServiceLeaderboardEntry>(10);
            var self = default(ServiceLeaderboardEntry);
            while (reader.Read())
            {
                var entry = new ServiceLeaderboardEntry
                {
                    UserName = reader.GetString(reader.GetOrdinal("user_name")),
                    Contracts = _contracts.Select((c, i) => checked((uint)reader.GetInt64(reader.GetOrdinal("c" + i)))).ToArray(),
                    ServiceYears = checked((ulong)reader.GetInt64(reader.GetOrdinal("years"))),
                    Rank = reader.GetInt64(reader.GetOrdinal("position")),
                    IsSelf = reader.GetString(reader.GetOrdinal("account_id")) == accountId,
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
