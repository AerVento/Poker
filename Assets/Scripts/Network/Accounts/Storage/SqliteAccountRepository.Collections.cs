#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using Microsoft.Data.Sqlite;
using System.Linq;

namespace Game.Network.Accounts
{
    // Namecards and card faces had their own ownership tables until schema 15. The creators below keep the upgrade
    // path for older databases; MigrateToUnifiedItems then moves everything into account_items.
    public sealed partial class SqliteAccountRepository
    {
        private static void CreateShopSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            Execute(connection, transaction,
                "ALTER TABLE player_accounts ADD COLUMN equipped_namecard TEXT NOT NULL DEFAULT '';",
                @"CREATE TABLE account_namecards (
account_id TEXT NOT NULL REFERENCES player_accounts(account_id),
product_id TEXT NOT NULL,
purchased_at_utc TEXT NOT NULL,
PRIMARY KEY (account_id, product_id));",
                @"CREATE TABLE shop_requests (
account_id TEXT NOT NULL REFERENCES player_accounts(account_id),
operation_id TEXT NOT NULL,
fingerprint TEXT NOT NULL,
result INTEGER NOT NULL,
PRIMARY KEY (account_id, operation_id));");
        }

        private static void CreateCardFaceSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            var hasColumn = false;
            using (var columns = connection.CreateCommand())
            {
                columns.Transaction = transaction;
                columns.CommandText = "PRAGMA table_info(player_accounts);";
                using var reader = columns.ExecuteReader();
                while (reader.Read()) if (reader.GetString(1) == "equipped_card_face") hasColumn = true;
            }
            Execute(connection, transaction, new[]
            {
                hasColumn ? null : "ALTER TABLE player_accounts ADD COLUMN equipped_card_face TEXT NOT NULL DEFAULT '';",
                @"CREATE TABLE IF NOT EXISTS account_card_faces (
account_id TEXT NOT NULL REFERENCES player_accounts(account_id),
product_id TEXT NOT NULL,
purchased_at_utc TEXT NOT NULL,
PRIMARY KEY (account_id, product_id));",
                @"CREATE TABLE IF NOT EXISTS card_face_requests (
account_id TEXT NOT NULL REFERENCES player_accounts(account_id),
operation_id TEXT NOT NULL,
fingerprint TEXT NOT NULL,
result INTEGER NOT NULL,
PRIMARY KEY (account_id, operation_id));"
            }.Where(sql => sql != null).ToArray());
        }

        // Schema 15: namecards and card faces become ordinary items with namecard-/cardface- IDs, and the appearances every
        // account used to own implicitly (default namecard, classic and minimal card faces) become owned rows.
        // Equipped values store item IDs; an empty value still means the slot's default.
        private static void MigrateToUnifiedItems(SqliteConnection connection, SqliteTransaction transaction)
        {
            const string upsert = " ON CONFLICT(account_id,item_id) DO UPDATE SET quantity=MAX(quantity,1);";
            Execute(connection, transaction,
                "INSERT INTO account_items(account_id,item_id,quantity) SELECT account_id,'namecard-'||product_id,1 " +
                    "FROM account_namecards WHERE product_id<>'default'" + upsert,
                "INSERT INTO account_items(account_id,item_id,quantity) SELECT account_id,'cardface-'||product_id,1 " +
                    "FROM account_card_faces WHERE product_id NOT IN ('default','minimal')" + upsert,
                "INSERT INTO account_items(account_id,item_id,quantity) SELECT account_id,'namecard-default',1 FROM player_accounts WHERE 1" + upsert,
                "INSERT INTO account_items(account_id,item_id,quantity) SELECT account_id,'cardface-default',1 FROM player_accounts WHERE 1" + upsert,
                "INSERT INTO account_items(account_id,item_id,quantity) SELECT account_id,'cardface-minimal',1 FROM player_accounts WHERE 1" + upsert,
                "UPDATE player_accounts SET equipped_namecard='' WHERE equipped_namecard='default';",
                "UPDATE player_accounts SET equipped_namecard='namecard-'||equipped_namecard WHERE equipped_namecard<>'';",
                "UPDATE player_accounts SET equipped_card_face='' WHERE equipped_card_face='default';",
                "UPDATE player_accounts SET equipped_card_face='cardface-'||equipped_card_face WHERE equipped_card_face<>'';",
                // Old purchase receipts cannot be retried by the new protocol.
                "DROP TABLE account_namecards;",
                "DROP TABLE account_card_faces;",
                "DROP TABLE shop_requests;",
                "DROP TABLE card_face_requests;");
        }

        // Separate commands so every migration error is observed by this SQLite provider.
        private static void Execute(SqliteConnection connection, SqliteTransaction transaction, params string[] statements)
        {
            foreach (var sql in statements)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
#endif
