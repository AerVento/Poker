#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System.Collections.Generic;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : IStoredProductRepository
    {
        public StoredProductIds ReadStoredProductIds()
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                return new StoredProductIds
                {
                    Items = Distinct(connection, "SELECT DISTINCT item_id FROM account_items WHERE quantity > 0"),
                    Namecards = Distinct(connection, "SELECT DISTINCT product_id FROM account_namecards"),
                    CardFaces = Distinct(connection, "SELECT DISTINCT product_id FROM account_card_faces"),
                };
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
