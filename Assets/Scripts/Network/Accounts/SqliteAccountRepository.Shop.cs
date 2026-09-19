#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Events;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : INamecardRepository
    {
        private static void CreateShopSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            // Separate DDL commands so every migration error is observed by this SQLite provider.
            foreach (var sql in new[]
            {
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
PRIMARY KEY (account_id, operation_id));"
            })
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        public NamecardInventory ReadNamecards(string accountId)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                var account = FindById(connection, transaction, accountId);
                if (account == null)
                    return null;
                var ids = ReadOwnedNamecards(connection, transaction, accountId);
                var result = new NamecardInventory
                {
                    Account = account,
                    OwnedIds = ids.ToArray(),
                    CanAct = !account.LastRoomId.HasValue && !IsAccountInActiveHand(connection, transaction, accountId),
                };
                transaction.Commit();
                return result;
            }
        }

        private static List<string> ReadOwnedNamecards(SqliteConnection connection, SqliteTransaction transaction, string accountId)
        {
            var ids = new List<string> { "default" };
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT product_id FROM account_namecards WHERE account_id=$account ORDER BY product_id;";
            command.Parameters.AddWithValue("$account", accountId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                ids.Add(reader.GetString(0));
            return ids;
        }

        public ShopResult ChangeNamecard(string accountId, string operationId, ShopAction action,
            string productId, string catalogRevision, NamecardServerCatalog catalog) => ChangeNamecard(accountId, operationId, action, productId, catalogRevision, catalog, false, 0);

        public ShopResult ChangeNamecard(string accountId, string operationId, ShopAction action,
            string productId, string catalogRevision, NamecardServerCatalog catalog, bool authorizeChipConversion, uint authorizedShortfall)
        {
            if (!Guid.TryParseExact(operationId, "N", out _) ||
                !NamecardServerCatalog.IsValidId(productId) ||
                (action != ShopAction.Purchase && action != ShopAction.Equip))
                return ShopResult.InvalidRequest;
            var fingerprint = action + ":" + productId + ":" + catalogRevision;
            if (authorizeChipConversion) fingerprint += ":convert:" + authorizedShortfall;
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                using (var prior = connection.CreateCommand())
                {
                    prior.Transaction = transaction;
                    prior.CommandText = "SELECT fingerprint,result FROM shop_requests WHERE account_id=$account AND operation_id=$operation;";
                    prior.Parameters.AddWithValue("$account", accountId);
                    prior.Parameters.AddWithValue("$operation", operationId);
                    using var reader = prior.ExecuteReader();
                    if (reader.Read())
                        return reader.GetString(0) == fingerprint ? (ShopResult)reader.GetInt32(1) : ShopResult.InvalidRequest;
                }
                var account = FindById(connection, transaction, accountId);
                if (account == null)
                    return ShopResult.Unavailable;
                if (action == ShopAction.Equip && (account.LastRoomId.HasValue || IsAccountInActiveHand(connection, transaction, accountId)))
                    return ShopResult.OutsideRoomRequired;

                var product = catalog.Products.FirstOrDefault(p => p.Id == productId);
                var owned = ReadOwnedNamecards(connection, transaction, accountId).Contains(productId);
                var result = ShopResult.Success;
                if (product == null || (action == ShopAction.Purchase && !product.Listed))
                    result = ShopResult.UnknownProduct;
                else if (catalogRevision != catalog.Revision)
                    result = ShopResult.CatalogChanged;
                else if (action == ShopAction.Purchase && owned)
                    result = ShopResult.AlreadyOwned;
                else if (action == ShopAction.Equip && !owned)
                    result = ShopResult.NotOwned;

                if (result == ShopResult.Success && action == ShopAction.Purchase)
                {
                    var payment = PrepareDollarPayment(connection, transaction, account, product.Price,
                        authorizeChipConversion, authorizedShortfall, "shop:" + operationId);
                    if (payment != WalletResult.Success)
                        return payment == WalletResult.ConversionRequired ? ShopResult.ConversionRequired :
                            payment == WalletResult.QuoteChanged ? ShopResult.QuoteChanged :
                            payment == WalletResult.BalanceLimit ? ShopResult.BalanceLimit : ShopResult.InsufficientChips;
                }

                if (result == ShopResult.Success)
                {
                    var now = DateTime.UtcNow;
                    var chips = action == ShopAction.Purchase ? account.Dollars - product.Price : account.Dollars;
                    using (var update = connection.CreateCommand())
                    {
                        update.Transaction = transaction;
                        update.CommandText = @"UPDATE player_accounts SET dollars=$chips,
equipped_namecard=$equipped, version=version+1, updated_at_utc=$now
WHERE account_id=$account AND version=$version;";
                        update.Parameters.AddWithValue("$chips", (long)chips);
                        update.Parameters.AddWithValue("$equipped", action == ShopAction.Equip
                            ? (productId == "default" ? "" : productId) : account.EquippedNamecardId ?? "");
                        update.Parameters.AddWithValue("$now", FormatDateTime(now));
                        update.Parameters.AddWithValue("$account", accountId);
                        update.Parameters.AddWithValue("$version", account.Version);
                        if (update.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Account changed during namecard transaction.");
                    }
                    if (action == ShopAction.Purchase)
                    {
                        using var inventory = connection.CreateCommand();
                        inventory.Transaction = transaction;
                        inventory.CommandText = "INSERT INTO account_namecards(account_id,product_id,purchased_at_utc) VALUES($account,$product,$now);";
                        inventory.Parameters.AddWithValue("$account", accountId);
                        inventory.Parameters.AddWithValue("$product", productId);
                        inventory.Parameters.AddWithValue("$now", FormatDateTime(now));
                        inventory.ExecuteNonQuery();
                        InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                            "shop:" + accountId + ":" + operationId, accountId, "ShopPurchase",
                            -(long)product.Price, account.Dollars, chips, null, null, now);
                    }
                    var writer = new TransactionEventWriter(connection, transaction);
                    var occurredAt = new DateTimeOffset(now).ToUnixTimeMilliseconds();
                    if (action == ShopAction.Purchase)
                        ShopEventSource.Purchased(writer, accountId, operationId, occurredAt, "namecard", productId, 1, product.Price);
                    else
                        CollectionEventSource.Equipped(writer, accountId, operationId, occurredAt, "namecard",
                            account.EquippedNamecardId, productId, productId, account.EquippedCardFaceId);
                }

                using (var receipt = connection.CreateCommand())
                {
                    receipt.Transaction = transaction;
                    receipt.CommandText = "INSERT INTO shop_requests(account_id,operation_id,fingerprint,result) VALUES($account,$operation,$fingerprint,$result);";
                    receipt.Parameters.AddWithValue("$account", accountId);
                    receipt.Parameters.AddWithValue("$operation", operationId);
                    receipt.Parameters.AddWithValue("$fingerprint", fingerprint);
                    receipt.Parameters.AddWithValue("$result", (int)result);
                    receipt.ExecuteNonQuery();
                }
                transaction.Commit();
                return result;
            }
        }
    }
}
#endif
