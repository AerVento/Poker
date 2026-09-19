#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Game.Events;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : IGiftRepository
    {
        public GiftUseExecution ConsumeGiftBatch(string accountId, string operationId,
            IReadOnlyList<GiftUseCommand> commands, ItemPurchaseRuntime runtime)
        {
            if (!Guid.TryParseExact(operationId, "N", out _) || runtime == null ||
                commands == null || commands.Count == 0 || commands.Count > GiftBatchLimits.MaxCommands)
                return new GiftUseExecution(GiftUseResult.InvalidRequest);

            ulong totalEffects = 0;
            foreach (var command in commands)
            {
                if (command == null || !NamecardServerCatalog.IsValidId(command.ItemId) ||
                    command.TargetSeatIndex < 0 || command.Quantity == 0)
                    return new GiftUseExecution(GiftUseResult.InvalidRequest);
                totalEffects += command.Quantity;
                if (totalEffects > GiftBatchLimits.MaxEffects)
                    return new GiftUseExecution(GiftUseResult.InvalidRequest);
            }

            var fingerprint = JsonSerializer.Serialize(new
            {
                action = "gift-use-v2",
                commands = commands.Select(command => new
                {
                    command.ItemId,
                    command.TargetSeatIndex,
                    command.Quantity,
                }).ToArray(),
                runtime.Revision,
            });

            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                using (var prior = connection.CreateCommand())
                {
                    prior.Transaction = transaction;
                    prior.CommandText = "SELECT fingerprint,result FROM item_requests WHERE account_id=$account AND operation_id=$operation;";
                    prior.Parameters.AddWithValue("$account", accountId);
                    prior.Parameters.AddWithValue("$operation", operationId);
                    using var reader = prior.ExecuteReader();
                    if (reader.Read())
                    {
                        if (reader.GetString(0) != fingerprint)
                            return new GiftUseExecution(GiftUseResult.InvalidRequest);
                        return new GiftUseExecution((GiftUseResult)reader.GetInt32(1));
                    }
                }

                var account = FindById(connection, transaction, accountId);
                if (account == null)
                    return new GiftUseExecution(GiftUseResult.Unavailable);

                var owned = ReadItemStacks(connection, transaction, accountId)
                    .ToDictionary(stack => stack.ItemId, stack => stack.Quantity, StringComparer.Ordinal);
                owned.TryGetValue("devil-contract", out var devil);
                owned.TryGetValue("archdevil-contract", out var archdevil);
                owned.TryGetValue("satan-contract", out var satan);
                if (InfernalNamecard.IsForced(InfernalNamecard.CalculateYears(devil, archdevil, satan)))
                    return new GiftUseExecution(GiftUseResult.ServiceRestricted);
                var remaining = new Dictionary<string, uint>(owned, StringComparer.Ordinal);
                var applied = new List<GiftUseCommand>();
                var purchased = new Dictionary<string, uint>(StringComparer.Ordinal);
                var balance = account.Dollars;
                var result = ValidateGiftConsumption(account, commands, runtime);
                var now = DateTime.UtcNow;
                if (result == GiftUseResult.Success)
                {
                    foreach (var command in commands)
                    {
                        var product = runtime.Find(command.ItemId);
                        remaining.TryGetValue(command.ItemId, out var stock);
                        var fromStock = Math.Min(stock, command.Quantity);
                        var missing = command.Quantity - fromStock;
                        uint bought = 0;
                        if (missing > 0 && product.Policy.OnPurchaseEffects.Count == 0)
                        {
                            var affordable = product.Price == 0 ? missing : Math.Min(missing, balance / product.Price);
                            if (affordable > 0 && ItemPurchaseRuntime.CheckPurchase(
                                new PurchaseContext(accountId, balance, product, 0, account.TotalAssets),
                                false, affordable, out _) == ItemShopResult.Success)
                                bought = affordable;
                        }
                        balance -= checked(bought * product.Price);
                        remaining[command.ItemId] = stock - fromStock;
                        purchased.TryGetValue(command.ItemId, out var priorBought);
                        purchased[command.ItemId] = priorBought + bought;
                        var accepted = fromStock + bought;
                        if (accepted > 0)
                            applied.Add(new GiftUseCommand(command.ItemId, command.TargetSeatIndex, accepted));
                        // Keep the accepted prefix in click order, including a partial final command.
                        if (accepted < command.Quantity)
                            break;
                    }
                    if (applied.Count == 0)
                        result = GiftUseResult.InsufficientDollars;
                    foreach (var pair in remaining)
                    {
                        if (!owned.TryGetValue(pair.Key, out var original) || pair.Value == original)
                            continue;
                        using var inventory = connection.CreateCommand();
                        inventory.Transaction = transaction;
                        inventory.CommandText = @"UPDATE account_items SET quantity=$quantity
WHERE account_id=$account AND item_id=$item AND quantity=$owned;";
                        inventory.Parameters.AddWithValue("$quantity", (long)pair.Value);
                        inventory.Parameters.AddWithValue("$account", accountId);
                        inventory.Parameters.AddWithValue("$item", pair.Key);
                        inventory.Parameters.AddWithValue("$owned", (long)original);
                        if (inventory.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Gift inventory changed during transaction.");
                    }
                    if (balance != account.Dollars)
                    {
                        using var update = connection.CreateCommand();
                        update.Transaction = transaction;
                        update.CommandText = @"UPDATE player_accounts SET dollars=$balance,version=version+1,updated_at_utc=$now
WHERE account_id=$account AND version=$version;";
                        update.Parameters.AddWithValue("$balance", (long)balance);
                        update.Parameters.AddWithValue("$now", FormatDateTime(now));
                        update.Parameters.AddWithValue("$account", accountId);
                        update.Parameters.AddWithValue("$version", account.Version);
                        if (update.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Account changed during gift transaction.");
                        InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                            "gift:" + accountId + ":" + operationId, accountId, "GiftPurchase",
                            -(long)(account.Dollars - balance), account.Dollars, balance, null, null, now);
                    }
                }

                using (var receipt = connection.CreateCommand())
                {
                    receipt.Transaction = transaction;
                    receipt.CommandText = @"INSERT INTO item_requests(account_id,operation_id,fingerprint,result,created_at_utc,outcome_json)
VALUES($account,$operation,$fingerprint,$result,$now,$outcome);";
                    receipt.Parameters.AddWithValue("$account", accountId);
                    receipt.Parameters.AddWithValue("$operation", operationId);
                    receipt.Parameters.AddWithValue("$fingerprint", fingerprint);
                    receipt.Parameters.AddWithValue("$result", (int)result);
                    receipt.Parameters.AddWithValue("$now", FormatDateTime(now));
                    receipt.Parameters.AddWithValue("$outcome", JsonSerializer.Serialize(applied));
                    receipt.ExecuteNonQuery();
                }

                if (result == GiftUseResult.Success)
                {
                    var writer = new TransactionEventWriter(connection, transaction);
                    var occurredAt = new DateTimeOffset(now).ToUnixTimeMilliseconds();
                    for (var i = 0; i < applied.Count; i++)
                    {
                        var command = applied[i];
                        InventoryEventSource.Used(writer, accountId, operationId, occurredAt,
                            command.ItemId, command.Quantity, 0, command.TargetSeatIndex, i);
                    }
                    foreach (var pair in purchased.Where(pair => pair.Value > 0))
                        ShopEventSource.Purchased(writer, accountId, operationId + ":" + pair.Key, occurredAt,
                            "item", pair.Key, pair.Value, checked(pair.Value * runtime.Find(pair.Key).Price));
                    RecordInventoryChanged(connection, transaction, accountId, operationId, "gift-use", now);
                }
                transaction.Commit();
                return new GiftUseExecution(result, result == GiftUseResult.Success, applied);
            }
        }

        private static GiftUseResult ValidateGiftConsumption(PlayerAccount account,
            IReadOnlyList<GiftUseCommand> commands, ItemPurchaseRuntime runtime)
        {
            foreach (var command in commands)
            {
                var product = runtime.Find(command.ItemId);
                if (product == null)
                    return GiftUseResult.UnknownGift;
                // Availability is handled by the stock-first purchase plan, not by CheckUse's ownership gate.
                var useResult = ItemPurchaseRuntime.CheckUse(
                    new ItemUseContext(account.AccountId, account.Dollars, product, 1),
                    ItemUseLocation.StartedRoom);
                if (useResult != ItemShopResult.Success || product.UsePolicy.Kind != ItemUseKind.Gift ||
                    product.UsePolicy.Cost != 0 || product.UsePolicy.Effects.Count != 0)
                    return GiftUseResult.UseNotAllowed;
            }
            return GiftUseResult.Success;
        }
    }
}
#endif
