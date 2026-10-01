#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Game.Events;

namespace Game.Network.Accounts
{
    public sealed partial class SqliteAccountRepository : IItemRepository
    {
        private static void CreateItemSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            foreach (var sql in new[]
            {
                @"CREATE TABLE account_items (
account_id TEXT NOT NULL REFERENCES player_accounts(account_id),
item_id TEXT NOT NULL,
quantity INTEGER NOT NULL CHECK(quantity >= 0 AND quantity <= 4294967295),
PRIMARY KEY(account_id, item_id));",
                @"CREATE TABLE item_requests (
account_id TEXT NOT NULL REFERENCES player_accounts(account_id),
operation_id TEXT NOT NULL,
fingerprint TEXT NOT NULL,
result INTEGER NOT NULL,
created_at_utc TEXT NOT NULL,
PRIMARY KEY(account_id, operation_id));"
            })
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        public AccountItemInventory ReadItems(string accountId)
        {
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                var account = FindById(connection, transaction, accountId);
                if (account == null) return null;
                var snapshot = new AccountItemInventory
                {
                    Account = account,
                    Items = ReadItemStacks(connection, transaction, accountId),
                    CanAct = true
                };
                transaction.Commit();
                return snapshot;
            }
        }

        private static ItemStack[] ReadItemStacks(SqliteConnection connection, SqliteTransaction transaction, string accountId)
        {
            var items = new List<ItemStack>();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT item_id,quantity FROM account_items WHERE account_id=$account AND quantity>0 ORDER BY item_id;";
            command.Parameters.AddWithValue("$account", accountId);
            using var reader = command.ExecuteReader();
            while (reader.Read()) items.Add(new ItemStack(reader.GetString(0), checked((uint)reader.GetInt64(1))));
            return items.ToArray();
        }

        private static void CreateItemOutcomeSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "ALTER TABLE item_requests ADD COLUMN outcome_json TEXT NULL;";
            command.ExecuteNonQuery();
        }

        public ItemChangeExecution ChangeItem(string accountId, string operationId, ItemShopAction action,
            string itemId, uint quantity, uint quotedPrice, string catalogRevision, ItemPurchaseRuntime runtime, bool inRoom = false) => ChangeItem(accountId, operationId, action, itemId, quantity, quotedPrice, catalogRevision, runtime, inRoom, false, 0);

        public ItemChangeExecution ChangeItem(string accountId, string operationId, ItemShopAction action,
            string itemId, uint quantity, uint quotedPrice, string catalogRevision, ItemPurchaseRuntime runtime, bool inRoom,
            bool authorizeChipConversion, uint authorizedShortfall)
        {
            if (!Guid.TryParseExact(operationId, "N", out _) || !NamecardServerCatalog.IsValidId(itemId) ||
                quantity == 0 || (action != ItemShopAction.Purchase && action != ItemShopAction.Use) ||
                string.IsNullOrEmpty(catalogRevision) || catalogRevision.Length > 64 ||
                (action == ItemShopAction.Use && (quotedPrice != 0 || quantity != 1)))
                return new ItemChangeExecution(ItemShopResult.InvalidRequest);
            if (runtime == null) return new ItemChangeExecution(ItemShopResult.Unavailable);
            // Preserve schema-6 request fingerprints so old receipts still match exact retries.
            var fingerprint = JsonSerializer.Serialize(new { action, itemId, quantity, quotedPrice, catalogRevision });
            if (authorizeChipConversion) fingerprint += ":convert:" + authorizedShortfall;
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                using (var prior = connection.CreateCommand())
                {
                    prior.Transaction = transaction;
                    prior.CommandText = "SELECT fingerprint,result,outcome_json FROM item_requests WHERE account_id=$account AND operation_id=$operation;";
                    prior.Parameters.AddWithValue("$account", accountId);
                    prior.Parameters.AddWithValue("$operation", operationId);
                    using var reader = prior.ExecuteReader();
                    if (reader.Read())
                    {
                        if (reader.GetString(0) != fingerprint) return new ItemChangeExecution(ItemShopResult.InvalidRequest);
                        return new ItemChangeExecution((ItemShopResult)reader.GetInt32(1),
                            reader.IsDBNull(2) ? null : JsonSerializer.Deserialize<ItemPurchaseOutcome>(reader.GetString(2)));
                    }
                }
                var account = FindById(connection, transaction, accountId);
                if (account == null) return new ItemChangeExecution(ItemShopResult.Unavailable);
                var product = runtime.Find(itemId);
                var owned = ReadItemStacks(connection, transaction, accountId).FirstOrDefault(p => p.ItemId == itemId)?.Quantity ?? 0;
                bool outsideRoom = !inRoom && !account.LastRoomId.HasValue && !IsAccountInActiveHand(connection, transaction, accountId);
                if (product != null && catalogRevision == runtime.Revision &&
                    (action != ItemShopAction.Purchase || quotedPrice == product.Price))
                {
                    var cost = action == ItemShopAction.Purchase ? (ulong)product.Price * quantity : product.UsePolicy?.Cost ?? 0;
                    var hypothetical = new PurchaseContext(accountId, (uint)Math.Min(uint.MaxValue, Math.Max((ulong)account.Dollars, cost)), product, owned, account.TotalAssets);
                    var eligibility = ValidateItemChange(hypothetical, action, quantity, quotedPrice, catalogRevision, runtime, outsideRoom, out _);
                    if (eligibility == ItemShopResult.Success)
                    {
                        var payment = PrepareDollarPayment(connection, transaction, account, cost,
                            authorizeChipConversion, authorizedShortfall, "item:" + operationId);
                        if (payment != WalletResult.Success)
                            return new ItemChangeExecution(payment == WalletResult.ConversionRequired ? ItemShopResult.ConversionRequired :
                                payment == WalletResult.QuoteChanged ? ItemShopResult.QuoteChanged :
                                payment == WalletResult.BalanceLimit ? ItemShopResult.BalanceLimit : ItemShopResult.InsufficientChips);
                    }
                }
                var context = new PurchaseContext(accountId, account.Dollars, product, owned, account.TotalAssets);
                var result = ValidateItemChange(context, action, quantity, quotedPrice, catalogRevision, runtime, outsideRoom, out var failedCondition);
                var now = DateTime.UtcNow;
                uint finalBalance = account.Dollars;
                var effects = new List<ItemEffectOutcome>();
                if (result == ItemShopResult.Success)
                {
                    // Plan only after all conditions pass. Build and validate all commands before any write.
                    result = action == ItemShopAction.Purchase ? PlanItemEffects(context, quantity, effects, out finalBalance) :
                        PlanItemUseEffects(new ItemUseContext(accountId, account.Dollars, product, owned), effects, out finalBalance);
                }
                if (result == ItemShopResult.Success)
                {
                    if (finalBalance != account.Dollars)
                    {
                        using var update = connection.CreateCommand();
                        update.Transaction = transaction;
                        update.CommandText = @"UPDATE player_accounts SET dollars=$chips,version=version+1,updated_at_utc=$now
WHERE account_id=$account AND version=$version;";
                        update.Parameters.AddWithValue("$chips", (long)finalBalance);
                        update.Parameters.AddWithValue("$now", FormatDateTime(now));
                        update.Parameters.AddWithValue("$account", accountId);
                        update.Parameters.AddWithValue("$version", account.Version);
                        if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Account changed during item transaction.");
                    }
                    using (var inventory = connection.CreateCommand())
                    {
                        inventory.Transaction = transaction;
                        inventory.CommandText = @"INSERT INTO account_items(account_id,item_id,quantity) VALUES($account,$item,$quantity)
ON CONFLICT(account_id,item_id) DO UPDATE SET quantity=excluded.quantity;";
                        inventory.Parameters.AddWithValue("$account", accountId);
                        inventory.Parameters.AddWithValue("$item", itemId);
                        inventory.Parameters.AddWithValue("$quantity", (long)(action == ItemShopAction.Purchase ? checked(owned + quantity) : checked(owned - 1)));
                        inventory.ExecuteNonQuery();
                    }
                    if (action == ItemShopAction.Purchase)
                        InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                            "item:" + accountId + ":" + operationId, accountId,
                            "ItemPurchase", -checked((long)((ulong)product.Price * quantity)),
                            account.Dollars, checked((uint)(account.Dollars - (ulong)product.Price * quantity)), null, null, now);
                    foreach (var effect in effects)
                        InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                            "item-effect:" + accountId + ":" + operationId + ":" + effect.EffectIndex + ":" + effect.CommandIndex,
                            accountId, effect.Kind == PurchaseEffectKind.CreditChips ? "ItemPurchaseReward" : "ItemUsePayment",
                            effect.Kind == PurchaseEffectKind.CreditChips ? (long)effect.Amount : -(long)effect.Amount,
                            effect.BalanceBefore, effect.BalanceAfter, null, null, now);
                }
                if (result != ItemShopResult.Success) return new ItemChangeExecution(result);
                var outcome = new ItemPurchaseOutcome(2, itemId, result == ItemShopResult.Success && action == ItemShopAction.Purchase
                        ? checked((uint)((ulong)product.Price * quantity)) : 0,
                    account.Dollars, result == ItemShopResult.Success ? finalBalance : account.Dollars, runtime.Revision,
                    failedCondition, result == ItemShopResult.Success ? effects : Array.Empty<ItemEffectOutcome>());
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
                    receipt.Parameters.AddWithValue("$outcome", JsonSerializer.Serialize(outcome));
                    receipt.ExecuteNonQuery();
                }
                if (result == ItemShopResult.Success)
                {
                    var writer = new TransactionEventWriter(connection, transaction);
                    var occurredAt = new DateTimeOffset(now).ToUnixTimeMilliseconds();
                    if (action == ItemShopAction.Purchase)
                        ShopEventSource.Purchased(writer, accountId, operationId, occurredAt, "item", itemId, quantity, outcome.ChargedPrice);
                    else
                        InventoryEventSource.Used(writer, accountId, operationId, occurredAt, itemId, 1, checked(account.Dollars - finalBalance));
                    RecordInventoryChanged(connection, transaction, accountId, operationId,
                        action == ItemShopAction.Purchase ? "purchase" : "use", now);
                }
                transaction.Commit();
                return new ItemChangeExecution(result, outcome, result == ItemShopResult.Success);
            }
        }

        private static ItemShopResult PlanItemUseEffects(ItemUseContext context, List<ItemEffectOutcome> results, out uint balance)
        {
            balance = context.BalanceBefore;
            var policy = context.Product.UsePolicy;
            for (var effectIndex = 0; effectIndex < policy.Effects.Count; effectIndex++)
            {
                var effect = policy.Effects[effectIndex];
                var commands = effect.Plan(context);
                if (commands == null || commands.Count == 0 || commands.Count > 16)
                    throw new InvalidOperationException("Invalid item use command plan.");
                ulong amount = 0;
                for (var commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    var debit = commands[commandIndex] ?? throw new InvalidOperationException("Missing item use command.");
                    if (balance < debit.Amount) return ItemShopResult.InsufficientChips;
                    var next = checked(balance - debit.Amount);
                    results.Add(new ItemEffectOutcome(effectIndex, commandIndex, PurchaseEffectKind.DebitChips,
                        effect.Version, debit.Amount, balance, next));
                    balance = next;
                    amount += debit.Amount;
                }
                if (amount != effect.Amount) throw new InvalidOperationException("Item use effect differs from its quote.");
            }
            return ItemShopResult.Success;
        }

        private static ItemShopResult PlanItemEffects(PurchaseContext context, uint quantity,
            List<ItemEffectOutcome> results, out uint balance)
        {
            balance = checked((uint)(context.BalanceBefore - (ulong)context.Product.Price * quantity));
            var policy = context.Product.Policy;
            for (int effectIndex = 0; effectIndex < policy.OnPurchaseEffects.Count; effectIndex++)
            {
                var commands = policy.OnPurchaseEffects[effectIndex].Plan(context);
                if (commands == null || commands.Count == 0 || commands.Count > 16)
                    throw new InvalidOperationException("Invalid item effect command plan.");
                ulong amount = 0;
                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    if (!(commands[commandIndex] is CreditChips credit))
                        throw new InvalidOperationException("Unsupported item effect command.");
                    if ((ulong)balance + credit.Amount > uint.MaxValue) return ItemShopResult.BalanceLimit;
                    var next = checked(balance + credit.Amount);
                    results.Add(new ItemEffectOutcome(effectIndex, commandIndex, PurchaseEffectKind.CreditChips,
                        policy.EffectDescriptors[effectIndex].Version, credit.Amount, balance, next));
                    balance = next;
                    amount += credit.Amount;
                }
                if (amount != policy.EffectDescriptors[effectIndex].Amount)
                    throw new InvalidOperationException("Item effect differs from its authoritative quote.");
            }
            return ItemShopResult.Success;
        }

        private static ItemShopResult ValidateItemChange(PurchaseContext context, ItemShopAction action,
            uint quantity, uint quotedPrice, string revision, ItemPurchaseRuntime runtime, bool outsideRoom,
            out PurchaseConditionDescriptor failedCondition)
        {
            failedCondition = null;
            var product = context.Product;
            if (product == null || (action == ItemShopAction.Purchase && !product.Listed)) return ItemShopResult.UnknownItem;
            if (revision != runtime.Revision || (action == ItemShopAction.Purchase && quotedPrice != product.Price))
                return ItemShopResult.CatalogChanged;
            if (action == ItemShopAction.Purchase)
                return ItemPurchaseRuntime.CheckPurchase(context, outsideRoom, quantity, out failedCondition);
            return ItemPurchaseRuntime.CheckUse(new ItemUseContext(context.AccountId, context.BalanceBefore,
                product, context.OwnedQuantity), outsideRoom);
        }
    }
}
#endif
