#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System.Collections.Generic;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : IStoredItemRepository
    {
        public string[] ReadStoredItemIds()
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                return Distinct(connection, "SELECT DISTINCT item_id FROM account_items WHERE quantity > 0");
            }
        }

        private static string[] Distinct(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
        {
            var ids = new List<string>();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            while (reader.Read()) ids.Add(reader.GetString(0));
            return ids.ToArray();
        }
    }
}
#endif
