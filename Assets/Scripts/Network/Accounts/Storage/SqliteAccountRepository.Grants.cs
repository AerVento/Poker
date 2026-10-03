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
            uint quantity, GameCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(accountId) || !Guid.TryParseExact(operationId, "N", out _) || quantity == 0 ||
                !GameCatalog.IsValidId(itemId))
                return new ItemGrantExecution(ItemGrantResult.InvalidRequest);
            if (catalog == null)
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
                var item = catalog.FindItem(itemId);
                var owned = item == null ? 0u :
                    ReadItemStacks(connection, transaction, accountId).FirstOrDefault(stack => stack.ItemId == itemId)?.Quantity ?? 0;
                var grantResult = item == null ? ItemGrantResult.UnknownItem : owned >= item.MaxQuantity
                    ? (item.MaxQuantity == 1 ? ItemGrantResult.AlreadyOwned : ItemGrantResult.QuantityLimit)
                    : ItemGrantResult.Success;
                var now = DateTime.UtcNow;
                if (grantResult == ItemGrantResult.Success)
                {
                    var reward = new Reward(new[] { new RewardEntry { Kind = RewardItemKind.Item, ItemId = itemId, Quantity = quantity } });
                    var result = ApplyReward(connection, transaction, account, reward,
                        catalog.Items.ToDictionary(i => i.Id, i => i.MaxQuantity, StringComparer.Ordinal), "grant:" + operationId, "ItemGrant", out _);
                    if (result != RewardGrantStatus.Success) throw new InvalidOperationException("Invalid item grant.");
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
                transaction.Commit();
                return new ItemGrantExecution(grantResult, grantResult == ItemGrantResult.Success);
            }
        }

    }
}
#endif
