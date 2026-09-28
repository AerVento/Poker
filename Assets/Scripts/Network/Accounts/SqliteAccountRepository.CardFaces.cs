#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Events;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : ICardFaceRepository
    {
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
            foreach (var sql in new[]
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
            }.Where(sql => sql != null))
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        public CardFaceInventory ReadCardFaces(string accountId)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                var account = FindById(connection, transaction, accountId);
                if (account == null)
                    return null;
                var ids = ReadOwnedCardFaces(connection, transaction, accountId);
                var result = new CardFaceInventory
                {
                    Account = account,
                    OwnedIds = ids.ToArray(),
                    CanAct = !account.LastRoomId.HasValue && !IsAccountInActiveHand(connection, transaction, accountId),
                };
                transaction.Commit();
                return result;
            }
        }

        private static List<string> ReadOwnedCardFaces(SqliteConnection connection, SqliteTransaction transaction, string accountId)
        {
            var ids = new List<string>(CardFaceItemId.DefaultOwnedIds);
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT product_id FROM account_card_faces WHERE account_id=$account ORDER BY product_id;";
            command.Parameters.AddWithValue("$account", accountId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetString(0);
                if (!ids.Contains(id)) ids.Add(id);
            }
            return ids;
        }

        public ShopResult ChangeCardFace(string accountId, string operationId, ShopAction action,
            string productId, string catalogRevision, CardFaceServerCatalog catalog) => ChangeCardFace(accountId, operationId, action, productId, catalogRevision, catalog, false, 0);

        public ShopResult ChangeCardFace(string accountId, string operationId, ShopAction action,
            string productId, string catalogRevision, CardFaceServerCatalog catalog, bool authorizeChipConversion, uint authorizedShortfall)
        {
            if (!Guid.TryParseExact(operationId, "N", out _) ||
                !CardFaceServerCatalog.IsValidId(productId) ||
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
                    prior.CommandText = "SELECT fingerprint,result FROM card_face_requests WHERE account_id=$account AND operation_id=$operation;";
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
                var owned = ReadOwnedCardFaces(connection, transaction, accountId).Contains(productId);
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
                        authorizeChipConversion, authorizedShortfall, "cardfaces:" + operationId);
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
equipped_card_face=$equipped, version=version+1, updated_at_utc=$now
WHERE account_id=$account AND version=$version;";
                        update.Parameters.AddWithValue("$chips", (long)chips);
                        update.Parameters.AddWithValue("$equipped", action == ShopAction.Equip
                            ? (productId == "default" ? "" : productId) : account.EquippedCardFaceId ?? "");
                        update.Parameters.AddWithValue("$now", FormatDateTime(now));
                        update.Parameters.AddWithValue("$account", accountId);
                        update.Parameters.AddWithValue("$version", account.Version);
                        if (update.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Account changed during card_face transaction.");
                    }
                    if (action == ShopAction.Purchase)
                    {
                        using var inventory = connection.CreateCommand();
                        inventory.Transaction = transaction;
                        inventory.CommandText = "INSERT INTO account_card_faces(account_id,product_id,purchased_at_utc) VALUES($account,$product,$now);";
                        inventory.Parameters.AddWithValue("$account", accountId);
                        inventory.Parameters.AddWithValue("$product", productId);
                        inventory.Parameters.AddWithValue("$now", FormatDateTime(now));
                        inventory.ExecuteNonQuery();
                        InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                            "card-face:" + accountId + ":" + operationId, accountId, "CardFacePurchase",
                            -(long)product.Price, account.Dollars, chips, null, null, now);
                    }
                    var writer = new TransactionEventWriter(connection, transaction);
                    var occurredAt = new DateTimeOffset(now).ToUnixTimeMilliseconds();
                    if (action == ShopAction.Purchase)
                        ShopEventSource.Purchased(writer, accountId, operationId, occurredAt, "card-face", productId, 1, product.Price);
                    else
                        CollectionEventSource.Equipped(writer, accountId, operationId, occurredAt, "card-face",
                            account.EquippedCardFaceId, productId, account.EquippedNamecardId, productId);
                    if (action == ShopAction.Purchase)
                        RecordAssetsChanged(connection, transaction, accountId, operationId, "card-face-purchase", now);
                }

                using (var receipt = connection.CreateCommand())
                {
                    receipt.Transaction = transaction;
                    receipt.CommandText = "INSERT INTO card_face_requests(account_id,operation_id,fingerprint,result) VALUES($account,$operation,$fingerprint,$result);";
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
