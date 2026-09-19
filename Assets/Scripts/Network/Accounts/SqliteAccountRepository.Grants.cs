#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using System;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : IItemGrantRepository
    {
        private static void CreateItemGrantSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"CREATE TABLE IF NOT EXISTS item_grant_receipts (
account_id TEXT NOT NULL REFERENCES player_accounts(account_id),
operation_id TEXT NOT NULL,
item_id TEXT NOT NULL,
quantity INTEGER NOT NULL CHECK(quantity > 0 AND quantity <= 4294967295),
result INTEGER NOT NULL,
created_at_utc TEXT NOT NULL,
PRIMARY KEY(account_id, operation_id));";
            command.ExecuteNonQuery();
        }

        public ItemGrantExecution GrantItem(string accountId, string itemId, string operationId,
            uint quantity, ItemIdentityRegistry registry)
        {
            if (string.IsNullOrWhiteSpace(accountId) || !Guid.TryParseExact(operationId, "N", out _) || quantity == 0 ||
                (!NamecardServerCatalog.IsValidId(itemId) && !NamecardItemId.TryToNamecardId(itemId, out _) &&
                 !CardFaceItemId.TryToCardFaceId(itemId, out _)))
                return new ItemGrantExecution(ItemGrantResult.InvalidRequest);
            if (registry == null)
                return new ItemGrantExecution(ItemGrantResult.Unavailable);

            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                // Check the persisted result before the current catalog: a completed grant may be retried after delisting/removal.
                using (var prior = connection.CreateCommand())
                {
                    prior.Transaction = transaction;
                    prior.CommandText = "SELECT item_id,quantity,result FROM item_grant_receipts WHERE account_id=$account AND operation_id=$operation;";
                    prior.Parameters.AddWithValue("$account", accountId);
                    prior.Parameters.AddWithValue("$operation", operationId);
                    using var reader = prior.ExecuteReader();
                    if (reader.Read())
                    {
                        var result = reader.GetString(0) == itemId && reader.GetInt64(1) == quantity
                            ? (ItemGrantResult)reader.GetInt32(2) : ItemGrantResult.InvalidRequest;
                        return new ItemGrantExecution(result);
                    }
                }

                var account = FindById(connection, transaction, accountId);
                if (account == null)
                    return new ItemGrantExecution(ItemGrantResult.AccountNotFound);
                registry.TryGet(itemId, out var identity);
                uint owned = 0;
                if (identity != null)
                {
                    owned = identity.IsNamecard
                        ? (ReadOwnedNamecards(connection, transaction, accountId).Contains(identity.NamecardId) ? 1u : 0u)
                        : identity.IsCardFace ? (ReadOwnedCardFaces(connection, transaction, accountId).Contains(identity.CardFaceId) ? 1u : 0u)
                        : ReadItemStacks(connection, transaction, accountId).FirstOrDefault(item => item.ItemId == itemId)?.Quantity ?? 0;
                }
                var grantResult = ValidateItemGrant(identity, owned, quantity);
                var now = DateTime.UtcNow;
                if (grantResult == ItemGrantResult.Success)
                {
                    using (var inventory = connection.CreateCommand())
                    {
                        inventory.Transaction = transaction;
                        inventory.Parameters.AddWithValue("$account", accountId);
                        if (identity.IsNamecard)
                        {
                            // Share the same ownership row as Purchase/Equip. This never enters account_items.
                            inventory.CommandText = @"INSERT INTO account_namecards(account_id,product_id,purchased_at_utc)
VALUES($account,$product,$now);";
                            inventory.Parameters.AddWithValue("$product", identity.NamecardId);
                            inventory.Parameters.AddWithValue("$now", FormatDateTime(now));
                        }
                        else if (identity.IsCardFace)
                        {
                            inventory.CommandText = @"INSERT INTO account_card_faces(account_id,product_id,purchased_at_utc)
VALUES($account,$product,$now);";
                            inventory.Parameters.AddWithValue("$product", identity.CardFaceId);
                            inventory.Parameters.AddWithValue("$now", FormatDateTime(now));
                        }
                        else
                        {
                            inventory.CommandText = @"INSERT INTO account_items(account_id,item_id,quantity) VALUES($account,$item,$quantity)
ON CONFLICT(account_id,item_id) DO UPDATE SET quantity=excluded.quantity;";
                            inventory.Parameters.AddWithValue("$item", itemId);
                            inventory.Parameters.AddWithValue("$quantity", (long)checked(owned + quantity));
                        }
                        inventory.ExecuteNonQuery();
                    }
                    using var update = connection.CreateCommand();
                    update.Transaction = transaction;
                    // System grants may target offline/in-room players. Balance, room and equipped appearance stay intact.
                    update.CommandText = @"UPDATE player_accounts SET version=version+1,updated_at_utc=$now
WHERE account_id=$account AND version=$version;";
                    update.Parameters.AddWithValue("$account", accountId);
                    update.Parameters.AddWithValue("$now", FormatDateTime(now));
                    update.Parameters.AddWithValue("$version", account.Version);
                    if (update.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("Account changed during item grant.");
                }

                using (var receipt = connection.CreateCommand())
                {
                    receipt.Transaction = transaction;
                    receipt.CommandText = @"INSERT INTO item_grant_receipts(account_id,operation_id,item_id,quantity,result,created_at_utc)
VALUES($account,$operation,$item,$quantity,$result,$now);";
                    receipt.Parameters.AddWithValue("$account", accountId);
                    receipt.Parameters.AddWithValue("$operation", operationId);
                    receipt.Parameters.AddWithValue("$item", itemId);
                    receipt.Parameters.AddWithValue("$quantity", (long)quantity);
                    receipt.Parameters.AddWithValue("$result", (int)grantResult);
                    receipt.Parameters.AddWithValue("$now", FormatDateTime(now));
                    receipt.ExecuteNonQuery();
                }
                if (grantResult == ItemGrantResult.Success && !identity.IsNamecard && !identity.IsCardFace)
                    RecordInventoryChanged(connection, transaction, accountId, operationId, "grant", now);
                transaction.Commit();
                return new ItemGrantExecution(grantResult, grantResult == ItemGrantResult.Success);
            }
        }

        private static ItemGrantResult ValidateItemGrant(ItemIdentity identity, uint owned, uint quantity)
        {
            if (identity == null)
                return ItemGrantResult.UnknownItem;
            if (quantity > identity.MaxQuantity)
                return ItemGrantResult.QuantityLimit;
            if (identity.Kind == ItemKind.NonConsumable && owned > 0)
                return ItemGrantResult.AlreadyOwned;
            return (ulong)owned + quantity > identity.MaxQuantity ? ItemGrantResult.QuantityLimit : ItemGrantResult.Success;
        }
    }
}
#endif
