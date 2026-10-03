#if UNITY_EDITOR || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_WIN
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Game.Events;

namespace Game.Network.Accounts
{
    // Every owned item, namecards and card faces included, is one account_items row.
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

        private static void CreateItemOutcomeSchema(SqliteConnection connection, SqliteTransaction transaction)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "ALTER TABLE item_requests ADD COLUMN outcome_json TEXT NULL;";
            command.ExecuteNonQuery();
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

        private static uint OwnedQuantity(SqliteConnection connection, SqliteTransaction transaction, string accountId, string itemId) =>
            ReadItemStacks(connection, transaction, accountId).FirstOrDefault(stack => stack.ItemId == itemId)?.Quantity ?? 0;

        private static void SetItemQuantity(SqliteConnection connection, SqliteTransaction transaction, string accountId,
            string itemId, uint quantity)
        {
            using var inventory = connection.CreateCommand();
            inventory.Transaction = transaction;
            inventory.CommandText = @"INSERT INTO account_items(account_id,item_id,quantity) VALUES($account,$item,$quantity)
ON CONFLICT(account_id,item_id) DO UPDATE SET quantity=excluded.quantity;";
            inventory.Parameters.AddWithValue("$account", accountId);
            inventory.Parameters.AddWithValue("$item", itemId);
            inventory.Parameters.AddWithValue("$quantity", (long)quantity);
            inventory.ExecuteNonQuery();
        }

        private static void UpdateDollars(SqliteConnection connection, SqliteTransaction transaction, PlayerAccount account,
            uint dollars, DateTime now)
        {
            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = @"UPDATE player_accounts SET dollars=$dollars,version=version+1,updated_at_utc=$now
WHERE account_id=$account AND version=$version;";
            update.Parameters.AddWithValue("$dollars", (long)dollars);
            update.Parameters.AddWithValue("$now", FormatDateTime(now));
            update.Parameters.AddWithValue("$account", account.AccountId);
            update.Parameters.AddWithValue("$version", account.Version);
            if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Account changed during item transaction.");
        }

        private static bool TryReadReceipt(SqliteConnection connection, SqliteTransaction transaction, string accountId,
            string operationId, out string fingerprint, out ItemActionResult result, out string outcome)
        {
            using var prior = connection.CreateCommand();
            prior.Transaction = transaction;
            prior.CommandText = "SELECT fingerprint,result,outcome_json FROM item_requests WHERE account_id=$account AND operation_id=$operation;";
            prior.Parameters.AddWithValue("$account", accountId);
            prior.Parameters.AddWithValue("$operation", operationId);
            using var reader = prior.ExecuteReader();
            fingerprint = null;
            result = ItemActionResult.Unavailable;
            outcome = null;
            if (!reader.Read()) return false;
            fingerprint = reader.GetString(0);
            result = (ItemActionResult)reader.GetInt32(1);
            outcome = reader.IsDBNull(2) ? null : reader.GetString(2);
            return true;
        }

        private static void WriteReceipt(SqliteConnection connection, SqliteTransaction transaction, string accountId,
            string operationId, string fingerprint, ItemActionResult result, string outcome, DateTime now)
        {
            using var receipt = connection.CreateCommand();
            receipt.Transaction = transaction;
            receipt.CommandText = @"INSERT INTO item_requests(account_id,operation_id,fingerprint,result,created_at_utc,outcome_json)
VALUES($account,$operation,$fingerprint,$result,$now,$outcome);";
            receipt.Parameters.AddWithValue("$account", accountId);
            receipt.Parameters.AddWithValue("$operation", operationId);
            receipt.Parameters.AddWithValue("$fingerprint", fingerprint);
            receipt.Parameters.AddWithValue("$result", (int)result);
            receipt.Parameters.AddWithValue("$now", FormatDateTime(now));
            receipt.Parameters.AddWithValue("$outcome", (object)outcome ?? DBNull.Value);
            receipt.ExecuteNonQuery();
        }

        private static ItemActionResult ToItemResult(WalletResult payment) => payment switch
        {
            WalletResult.ConversionRequired => ItemActionResult.ConversionRequired,
            WalletResult.QuoteChanged => ItemActionResult.QuoteChanged,
            WalletResult.BalanceLimit => ItemActionResult.BalanceLimit,
            _ => ItemActionResult.InsufficientDollars,
        };

        public ItemChangeExecution PurchaseItem(string accountId, string operationId, string itemId, uint quantity,
            uint quotedPrice, string catalogRevision, ItemRules rules, bool authorizeChipConversion = false, uint authorizedShortfall = 0)
        {
            if (!Guid.TryParseExact(operationId, "N", out _) || !GameCatalog.IsValidId(itemId) || quantity == 0 ||
                string.IsNullOrEmpty(catalogRevision) || catalogRevision.Length > 64)
                return new ItemChangeExecution(ItemActionResult.InvalidRequest);
            if (rules == null) return new ItemChangeExecution(ItemActionResult.Unavailable);
            var fingerprint = JsonSerializer.Serialize(new { action = "purchase", itemId, quantity, quotedPrice, catalogRevision });
            if (authorizeChipConversion) fingerprint += ":convert:" + authorizedShortfall;
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                if (TryReadReceipt(connection, transaction, accountId, operationId, out var priorFingerprint, out var priorResult, out var priorOutcome))
                    return priorFingerprint != fingerprint ? new ItemChangeExecution(ItemActionResult.InvalidRequest) :
                        new ItemChangeExecution(priorResult, priorOutcome == null ? null : JsonSerializer.Deserialize<ItemPurchaseOutcome>(priorOutcome));
                var account = FindById(connection, transaction, accountId);
                if (account == null) return new ItemChangeExecution(ItemActionResult.Unavailable);
                var rule = rules.Find(itemId);
                var owned = OwnedQuantity(connection, transaction, accountId, itemId);
                var cost = (ulong)(rule?.Price ?? 0) * quantity;
                if (rule != null && catalogRevision == rules.Revision && quotedPrice == rule.Price)
                {
                    // Check everything except funds first, so a conversion is only made for an otherwise valid purchase.
                    var hypothetical = new PurchaseContext(accountId, (uint)Math.Min(uint.MaxValue, Math.Max((ulong)account.Dollars, cost)),
                        rule, owned, account.TotalAssets);
                    if (ItemRules.CheckPurchase(hypothetical, quantity, out _) == ItemActionResult.Success)
                    {
                        var payment = PrepareDollarPayment(connection, transaction, account, cost,
                            authorizeChipConversion, authorizedShortfall, "item:" + operationId);
                        if (payment != WalletResult.Success) return new ItemChangeExecution(ToItemResult(payment));
                    }
                }
                var context = new PurchaseContext(accountId, account.Dollars, rule, owned, account.TotalAssets);
                PurchaseConditionDescriptor failedCondition = null;
                var result = rule == null ? ItemActionResult.UnknownItem :
                    catalogRevision != rules.Revision || quotedPrice != rule.Price ? ItemActionResult.CatalogChanged :
                    ItemRules.CheckPurchase(context, quantity, out failedCondition);
                var now = DateTime.UtcNow;
                uint finalBalance = account.Dollars;
                var effects = new List<ItemEffectOutcome>();
                if (result == ItemActionResult.Success)
                    result = PlanItemEffects(context, quantity, effects, out finalBalance);
                if (result != ItemActionResult.Success) return new ItemChangeExecution(result);

                if (finalBalance != account.Dollars) UpdateDollars(connection, transaction, account, finalBalance, now);
                SetItemQuantity(connection, transaction, accountId, itemId, checked(owned + quantity));
                InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                    "item:" + accountId + ":" + operationId, accountId, "ItemPurchase", -checked((long)cost),
                    account.Dollars, checked((uint)(account.Dollars - cost)), null, null, now);
                foreach (var effect in effects)
                    InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                        "item-effect:" + accountId + ":" + operationId + ":" + effect.EffectIndex + ":" + effect.CommandIndex,
                        accountId, "ItemPurchaseReward", effect.Amount, effect.BalanceBefore, effect.BalanceAfter, null, null, now);
                var outcome = new ItemPurchaseOutcome(2, itemId, checked((uint)cost), account.Dollars, finalBalance,
                    rules.Revision, failedCondition, effects);
                WriteReceipt(connection, transaction, accountId, operationId, fingerprint, result, JsonSerializer.Serialize(outcome), now);
                var writer = new TransactionEventWriter(connection, transaction);
                ShopEventSource.Purchased(writer, accountId, operationId, new DateTimeOffset(now).ToUnixTimeMilliseconds(),
                    "item", itemId, quantity, outcome.ChargedPrice);
                RecordInventoryChanged(connection, transaction, accountId, operationId, "purchase", now);
                transaction.Commit();
                return new ItemChangeExecution(result, outcome, true);
            }
        }

        public ItemActionResult EquipItem(string accountId, string operationId, string itemId, ItemRules rules)
        {
            if (!Guid.TryParseExact(operationId, "N", out _) || !GameCatalog.IsValidId(itemId))
                return ItemActionResult.InvalidRequest;
            if (rules == null) return ItemActionResult.Unavailable;
            var fingerprint = "equip:" + itemId;
            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                if (TryReadReceipt(connection, transaction, accountId, operationId, out var priorFingerprint, out var priorResult, out _))
                    return priorFingerprint == fingerprint ? priorResult : ItemActionResult.InvalidRequest;
                var account = FindById(connection, transaction, accountId);
                if (account == null) return ItemActionResult.Unavailable;
                if (account.LastRoomId.HasValue || IsAccountInActiveHand(connection, transaction, accountId))
                    return ItemActionResult.OutsideRoomRequired;
                var rule = rules.Find(itemId);
                var result = rule == null ? ItemActionResult.UnknownItem : rule.Slot == null ? ItemActionResult.NotEquippable :
                    OwnedQuantity(connection, transaction, accountId, itemId) == 0 ? ItemActionResult.NotOwned : ItemActionResult.Success;
                if (result != ItemActionResult.Success) return result;

                var now = DateTime.UtcNow;
                var namecardSlot = rule.Slot == EquipSlot.Namecard;
                var stored = rule.IsDefaultAppearance ? string.Empty : itemId;
                var previous = namecardSlot ? account.EquippedNamecardId : account.EquippedCardFaceId;
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE player_accounts SET " + (namecardSlot ? "equipped_namecard" : "equipped_card_face") +
                        "=$equipped, version=version+1, updated_at_utc=$now WHERE account_id=$account AND version=$version;";
                    update.Parameters.AddWithValue("$equipped", stored);
                    update.Parameters.AddWithValue("$now", FormatDateTime(now));
                    update.Parameters.AddWithValue("$account", accountId);
                    update.Parameters.AddWithValue("$version", account.Version);
                    if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Account changed during equip transaction.");
                }
                WriteReceipt(connection, transaction, accountId, operationId, fingerprint, result, null, now);
                CollectionEventSource.Equipped(new TransactionEventWriter(connection, transaction), accountId, operationId,
                    new DateTimeOffset(now).ToUnixTimeMilliseconds(), namecardSlot ? "namecard" : "card-face", previous, stored,
                    namecardSlot ? stored : account.EquippedNamecardId, namecardSlot ? account.EquippedCardFaceId : stored);
                transaction.Commit();
                return result;
            }
        }

        public ItemConsumeExecution ConsumeItems(string accountId, string operationId, ItemConsumeMethod method,
            IReadOnlyList<ItemConsumeCommand> commands, string catalogRevision, ItemRules rules, ItemUseLocation location,
            bool authorizeChipConversion = false, uint authorizedShortfall = 0)
        {
            if (!Guid.TryParseExact(operationId, "N", out _) || !Enum.IsDefined(typeof(ItemConsumeMethod), method) ||
                commands == null || commands.Count == 0 || (catalogRevision?.Length ?? 0) > 64)
                return new ItemConsumeExecution(ItemActionResult.InvalidRequest);
            ulong total = 0;
            foreach (var command in commands)
            {
                if (command == null || !GameCatalog.IsValidId(command.ItemId) || command.Quantity == 0)
                    return new ItemConsumeExecution(ItemActionResult.InvalidRequest);
                total += command.Quantity;
            }
            // Use consumes one item per request; Give batches gifts sent in quick succession.
            if (method == ItemConsumeMethod.Use ? commands.Count != 1 || total != 1 || commands[0].TargetSeatIndex != -1 :
                commands.Count > GiftBatchLimits.MaxCommands || total > GiftBatchLimits.MaxEffects || commands.Any(c => c.TargetSeatIndex < 0))
                return new ItemConsumeExecution(ItemActionResult.InvalidRequest);
            if (rules == null) return new ItemConsumeExecution(ItemActionResult.Unavailable);
            var fingerprint = JsonSerializer.Serialize(new
            {
                action = "consume", method, catalogRevision,
                commands = commands.Select(c => new { c.ItemId, c.Quantity, c.TargetSeatIndex }).ToArray(),
            });
            if (authorizeChipConversion) fingerprint += ":convert:" + authorizedShortfall;

            lock (_syncRoot)
            {
                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();
                if (TryReadReceipt(connection, transaction, accountId, operationId, out var priorFingerprint, out var priorResult, out var priorOutcome))
                {
                    if (priorFingerprint != fingerprint) return new ItemConsumeExecution(ItemActionResult.InvalidRequest);
                    var applied = priorOutcome == null ? null : JsonSerializer.Deserialize<ConsumeReceipt[]>(priorOutcome)
                        .Select(c => new ItemConsumeCommand(c.ItemId, c.Quantity, c.TargetSeatIndex));
                    return new ItemConsumeExecution(priorResult, false, applied);
                }
                var account = FindById(connection, transaction, accountId);
                if (account == null) return new ItemConsumeExecution(ItemActionResult.Unavailable);
                var owned = ReadItemStacks(connection, transaction, accountId).ToDictionary(s => s.ItemId, s => s.Quantity, StringComparer.Ordinal);

                // Rules shared by every consume method.
                if (!string.IsNullOrEmpty(catalogRevision) && catalogRevision != rules.Revision)
                    return new ItemConsumeExecution(ItemActionResult.CatalogChanged);
                foreach (var command in commands)
                {
                    var rule = rules.Find(command.ItemId);
                    if (rule == null) return new ItemConsumeExecution(ItemActionResult.UnknownItem);
                    if (!rule.Consumable) return new ItemConsumeExecution(ItemActionResult.NotConsumable);
                    if (!rule.Supports(method)) return new ItemConsumeExecution(ItemActionResult.MethodUnavailable);
                }
                var execution = method == ItemConsumeMethod.Use
                    ? UseItem(connection, transaction, account, operationId, commands[0], rules, owned, location, authorizeChipConversion, authorizedShortfall)
                    : GiveItems(connection, transaction, account, operationId, commands, rules, owned, location);
                // A rejected request rolls back, including any chip conversion made while paying; a retry is evaluated again.
                if (execution.Result != ItemActionResult.Success) return execution;
                var now = DateTime.UtcNow;
                WriteReceipt(connection, transaction, accountId, operationId, fingerprint, execution.Result,
                    JsonSerializer.Serialize(execution.Applied.Select(c => new ConsumeReceipt
                        { ItemId = c.ItemId, Quantity = c.Quantity, TargetSeatIndex = c.TargetSeatIndex }).ToArray()), now);
                transaction.Commit();
                return execution;
            }
        }

        private sealed class ConsumeReceipt
        {
            public string ItemId { get; set; }
            public uint Quantity { get; set; }
            public int TargetSeatIndex { get; set; }
        }

        private static ItemConsumeExecution UseItem(SqliteConnection connection, SqliteTransaction transaction,
            PlayerAccount account, string operationId, ItemConsumeCommand command, ItemRules rules,
            IReadOnlyDictionary<string, uint> owned, ItemUseLocation location, bool authorizeChipConversion, uint authorizedShortfall)
        {
            var rule = rules.Find(command.ItemId);
            owned.TryGetValue(command.ItemId, out var quantity);
            var cost = rule.UsePolicy.Cost;
            // Check everything except funds first, so a conversion is only made for an otherwise valid use.
            var hypothetical = new ItemUseContext(account.AccountId, Math.Max(account.Dollars, cost), rule, quantity);
            if (ItemRules.CheckUse(hypothetical, location) == ItemActionResult.Success)
            {
                var payment = PrepareDollarPayment(connection, transaction, account, cost,
                    authorizeChipConversion, authorizedShortfall, "item:" + operationId);
                if (payment != WalletResult.Success) return new ItemConsumeExecution(ToItemResult(payment));
            }
            var context = new ItemUseContext(account.AccountId, account.Dollars, rule, quantity);
            var result = ItemRules.CheckUse(context, location);
            var effects = new List<ItemEffectOutcome>();
            var balance = account.Dollars;
            if (result == ItemActionResult.Success) result = PlanItemUseEffects(context, effects, out balance);
            if (result != ItemActionResult.Success) return new ItemConsumeExecution(result);

            var now = DateTime.UtcNow;
            if (balance != account.Dollars) UpdateDollars(connection, transaction, account, balance, now);
            SetItemQuantity(connection, transaction, account.AccountId, command.ItemId, quantity - 1);
            foreach (var effect in effects)
                InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                    "item-effect:" + account.AccountId + ":" + operationId + ":" + effect.EffectIndex + ":" + effect.CommandIndex,
                    account.AccountId, "ItemUsePayment", -(long)effect.Amount, effect.BalanceBefore, effect.BalanceAfter, null, null, now);
            var writer = new TransactionEventWriter(connection, transaction);
            InventoryEventSource.Used(writer, account.AccountId, operationId, new DateTimeOffset(now).ToUnixTimeMilliseconds(),
                command.ItemId, 1, checked(account.Dollars - balance));
            RecordInventoryChanged(connection, transaction, account.AccountId, operationId, "use", now);
            return new ItemConsumeExecution(ItemActionResult.Success, true, new[] { command });
        }

        // Gifts are taken from stock first; any shortfall is bought at the shop price when the player can pay.
        private static ItemConsumeExecution GiveItems(SqliteConnection connection, SqliteTransaction transaction,
            PlayerAccount account, string operationId, IReadOnlyList<ItemConsumeCommand> commands, ItemRules rules,
            IReadOnlyDictionary<string, uint> owned, ItemUseLocation location)
        {
            if (location != ItemUseLocation.StartedRoom) return new ItemConsumeExecution(ItemActionResult.RoomStartedRequired);
            if (rules.Catalog.IsServing(rules.Catalog.ServiceYears(id => owned.TryGetValue(id, out var q) ? q : 0u)))
                return new ItemConsumeExecution(ItemActionResult.ServiceRestricted);
            var accountId = account.AccountId;
            var remaining = new Dictionary<string, uint>(owned, StringComparer.Ordinal);
            var applied = new List<ItemConsumeCommand>();
            var purchased = new Dictionary<string, uint>(StringComparer.Ordinal);
            var wallet = ReadWallet(connection, transaction, account);
            var purchasingPower = (uint)Math.Min(uint.MaxValue,
                CurrencySystem.PurchasingPower(account.Dollars, wallet.AvailableChips, wallet.CanExchange));
            var balance = purchasingPower;
            foreach (var command in commands)
            {
                var rule = rules.Find(command.ItemId);
                remaining.TryGetValue(command.ItemId, out var stock);
                var fromStock = Math.Min(stock, command.Quantity);
                var missing = command.Quantity - fromStock;
                uint bought = 0;
                if (missing > 0 && rule.Policy.OnPurchaseEffects.Count == 0)
                {
                    var affordable = rule.Price == 0 ? missing : Math.Min(missing, balance / rule.Price);
                    if (affordable > 0 && ItemRules.CheckPurchase(
                        new PurchaseContext(accountId, balance, rule, 0, account.TotalAssets), affordable, out _) == ItemActionResult.Success)
                        bought = affordable;
                }
                balance -= checked(bought * rule.Price);
                remaining[command.ItemId] = stock - fromStock;
                purchased.TryGetValue(command.ItemId, out var priorBought);
                purchased[command.ItemId] = priorBought + bought;
                var accepted = fromStock + bought;
                if (accepted > 0)
                    applied.Add(new ItemConsumeCommand(command.ItemId, accepted, command.TargetSeatIndex));
                // Keep the accepted prefix in click order, including a partial final command.
                if (accepted < command.Quantity)
                    break;
            }
            if (applied.Count == 0) return new ItemConsumeExecution(ItemActionResult.InsufficientDollars);
            var cost = purchasingPower - balance;
            var payment = PrepareDollarPayment(connection, transaction, account, cost, false, 0, "gift:" + operationId);
            if (payment != WalletResult.Success) return new ItemConsumeExecution(ItemActionResult.InsufficientDollars);
            balance = account.Dollars - cost;
            var now = DateTime.UtcNow;
            foreach (var pair in remaining)
            {
                if (!owned.TryGetValue(pair.Key, out var original) || pair.Value == original) continue;
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
                UpdateDollars(connection, transaction, account, balance, now);
                InsertDollarLedger(connection, transaction, Guid.NewGuid().ToString("N"),
                    "gift:" + accountId + ":" + operationId, accountId, "GiftPurchase",
                    -(long)(account.Dollars - balance), account.Dollars, balance, null, null, now);
            }
            var writer = new TransactionEventWriter(connection, transaction);
            var occurredAt = new DateTimeOffset(now).ToUnixTimeMilliseconds();
            for (var i = 0; i < applied.Count; i++)
                InventoryEventSource.Used(writer, accountId, operationId, occurredAt,
                    applied[i].ItemId, applied[i].Quantity, 0, applied[i].TargetSeatIndex, i);
            foreach (var pair in purchased.Where(pair => pair.Value > 0))
                ShopEventSource.Purchased(writer, accountId, operationId + ":" + pair.Key, occurredAt,
                    "item", pair.Key, pair.Value, checked(pair.Value * rules.Find(pair.Key).Price));
            RecordInventoryChanged(connection, transaction, accountId, operationId, "gift-use", now);
            return new ItemConsumeExecution(ItemActionResult.Success, true, applied);
        }

        private static ItemActionResult PlanItemUseEffects(ItemUseContext context, List<ItemEffectOutcome> results, out uint balance)
        {
            balance = context.BalanceBefore;
            var policy = context.Item.UsePolicy;
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
                    if (balance < debit.Amount) return ItemActionResult.InsufficientDollars;
                    var next = checked(balance - debit.Amount);
                    results.Add(new ItemEffectOutcome(effectIndex, commandIndex, PurchaseEffectKind.DebitChips,
                        effect.Version, debit.Amount, balance, next));
                    balance = next;
                    amount += debit.Amount;
                }
                if (amount != effect.Amount) throw new InvalidOperationException("Item use effect differs from its quote.");
            }
            return ItemActionResult.Success;
        }

        private static ItemActionResult PlanItemEffects(PurchaseContext context, uint quantity,
            List<ItemEffectOutcome> results, out uint balance)
        {
            balance = checked((uint)(context.BalanceBefore - (ulong)context.Item.Price * quantity));
            var policy = context.Item.Policy;
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
                    if ((ulong)balance + credit.Amount > uint.MaxValue) return ItemActionResult.BalanceLimit;
                    var next = checked(balance + credit.Amount);
                    results.Add(new ItemEffectOutcome(effectIndex, commandIndex, PurchaseEffectKind.CreditChips,
                        policy.EffectDescriptors[effectIndex].Version, credit.Amount, balance, next));
                    balance = next;
                    amount += credit.Amount;
                }
                if (amount != policy.EffectDescriptors[effectIndex].Amount)
                    throw new InvalidOperationException("Item effect differs from its authoritative quote.");
            }
            return ItemActionResult.Success;
        }
    }
}
#endif
